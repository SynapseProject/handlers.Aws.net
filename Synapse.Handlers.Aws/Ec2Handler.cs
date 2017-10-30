using Amazon.EC2;
using Amazon.EC2.Model;
using AutoMapper;
using Newtonsoft.Json;
using Synapse.Aws.Core;
using Synapse.Core;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json.Converters;
using YamlDotNet.Serialization;
using StatusType = Synapse.Core.StatusType;
using Synapse.Handlers.Aws;

public class Ec2Handler : HandlerRuntimeBase
{
    private HandlerConfig _config;
    public override object GetConfigInstance()
    {
        return new HandlerConfig
        {
            SmtpServer = "xxxxxx.xxx.com",
            AwsEnvironmentProfile = new Dictionary<string, string>
            {
                { "ENV1", "AWSPROFILE1" },
                { "ENV2", "AWSPROFILE2" }
            }
        };
    }
    private readonly ExecuteResult _result = new ExecuteResult()
    {
        Status = StatusType.None,
        BranchStatus = StatusType.None,
        Sequence = int.MaxValue
    };
    private readonly Ec2Response _response = new Ec2Response();
    private int _sequenceNumber = 0;
    private string _mainProgressMsg = "";
    private string _context = "Execute";
    private bool _encounteredFailure = false;
    private string _returnFormat = "json"; // Default return format

    public override object GetParametersInstance()
    {
        return new Ec2Request()
        {
            Region = "eu-west-1",
            CloudEnvironment = "XXXXXX",
            RequestType = "instance-uptime",
            Action = "none",
            Uptime = new Ec2InstanceUptimeFilter()
            {
                Hours = 24,
                Operator = "ge"
            },
            MissingTags = new List<string>() { "Name", "Owner", "CostCentre" },
            Filters = new List<Filter>()
            {
                new Filter()
                {
                    Name = "tag:cloud-environment",
                    Values = { "XXXXXX", "YYYYYY", "ZZZZZZ" }
                },
                new Filter()
                {
                    Name = "instance-id",
                    Values = { "XXXXXX", "YYYYYY", "ZZZZZZ" }
                }
            },
            ReturnFormat = "json",
            Xslt = ""
        };
    }

    public override IHandlerRuntime Initialize(string values)
    {
        _config = DeserializeOrNew<HandlerConfig>( values );

        Mapper.Initialize( cfg =>
        {
            cfg.CreateMap<Instance, Ec2Instance>()
                .ForMember( d => d.Architecture, o => o.MapFrom( s => s.Architecture ) )
                .ForMember( d => d.AvailabilityZone, o => o.MapFrom( s => s.Placement.AvailabilityZone ) )
                .ForMember( d => d.CloudEnvironment, o => o.MapFrom( s => GetTagValue( "cloud-environment", s.Tags ) ) )
                .ForMember( d => d.CloudEnvironmentFriendlyName, o => o.MapFrom( s => GetTagValue( "cloud-environment-friendly-name", s.Tags ) ) )
                .ForMember( d => d.CostCentre, o => o.MapFrom( s => GetTagValue( "cost-centre", s.Tags ) ) )
                .ForMember( d => d.InstanceId, o => o.MapFrom( s => s.InstanceId ) )
                .ForMember( d => d.InstanceState, o => o.MapFrom( s => s.State.Name ) )
                .ForMember( d => d.InstanceType, o => o.MapFrom( s => s.InstanceType ) )
                .ForMember( d => d.Name, o => o.MapFrom( s => GetTagValue( "Name", s.Tags ) ) )
                .ForMember( d => d.LaunchTime, o => o.MapFrom( s => s.LaunchTime ) )
                .ForMember( d => d.PrivateDnsName, o => o.MapFrom( s => s.PrivateDnsName ) )
                .ForMember( d => d.PrivateIpAddress, o => o.MapFrom( s => s.PrivateIpAddress ) )
                .ForMember( d => d.PublicDnsName, o => o.MapFrom( s => s.PublicDnsName ) )
                .ForMember( d => d.PublicIpAddress, o => o.MapFrom( s => s.PublicIpAddress ) );
        } );
        return this;
    }

    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        string message;
        string xslt = "";
        try
        {
            message = "Deserializing incoming request...";
            UpdateProgress( message, StatusType.Initializing );
            string inputParameters = Utilities.RemoveParameterSingleQuote( startInfo.Parameters );
            Ec2Request parms = DeserializeOrNew<Ec2Request>( inputParameters );

            message = "Processing request...";
            UpdateProgress( message, StatusType.Running );
            ValidateRequest( parms );
            xslt = parms.Xslt;
            GetFilteredInstances( parms );

            message = "Querying against AWS has been processed" + (_encounteredFailure ? " with error" : "") + ".";
            UpdateProgress( message, _encounteredFailure ? StatusType.CompletedWithErrors : StatusType.Success );
            _response.Summary = message;
            _response.ExitCode = _encounteredFailure ? -1 : 0;
            // xslt = File.ReadAllText( @"c:\temp\XML Essentials\matchingec2.xslt" );
        }
        catch ( Exception ex )
        {
            UpdateProgress( ex.Message, StatusType.Failed );
            _encounteredFailure = true;
            _response.Summary = ex.Message;
            _response.ExitCode = -1;
        }
        finally
        {
            message = "Serializing response...";
            UpdateProgress( message );
            string xmlResponse = Utilities.SerializeXmlResponse( _response );
            string transformedXml = Utilities.TransformXml( xmlResponse, xslt );
            string serializedData = Utilities.SerializeTargetFormat( transformedXml, _returnFormat );
            _result.ExitData = serializedData;
        }

        return _result;
    }

    private void GetFilteredInstances(Ec2Request parms)
    {
        string profile;
        _config.AwsEnvironmentProfile.TryGetValue( parms.CloudEnvironment, out profile );
        List<Instance> instances = AwsServices.DescribeEc2Instances( parms.Filters, parms.Region, profile );
        List<Ec2Instance> resultInstances = Mapper.Map<List<Instance>, List<Ec2Instance>>( instances );

        _response.Ec2Instances = resultInstances;
        _response.Count = resultInstances.Count;
    }

    private void ProcessInstanceUptimeRequest(Ec2Request parms, bool isDryRun = false)
    {
        List<Ec2Instance> resultInstances = new List<Ec2Instance>();

        try
        {
            string profile;
            _config.AwsEnvironmentProfile.TryGetValue( parms.CloudEnvironment, out profile );
            List<Instance> instances = AwsServices.DescribeEc2Instances( null, parms.Region, profile );
            foreach ( Instance instance in instances )
            {
                if ( instance.State.Name == InstanceStateName.Running && InstanceUpFor( instance.LaunchTime, parms.Uptime.Hours, parms.Uptime.Operator ) )
                {
                    Ec2Instance mappedInstance = Mapper.Map<Instance, Ec2Instance>( instance );
                    resultInstances.Add( mappedInstance );
                }
            }
        }
        catch ( Exception ex )
        {
            _response.Summary = (isDryRun ? "Dry run has been completed. " : "") + ex.Message;
            _encounteredFailure = true;
        }

        _response.Ec2Instances = resultInstances;
        _response.Count = resultInstances.Count;
    }

    private void ProcessInstanceMissingTagsRequest(Ec2Request parms, bool isDryRun = false)
    {
        List<Ec2Instance> resultInstances = new List<Ec2Instance>();

        try
        {
            string profile;
            _config.AwsEnvironmentProfile.TryGetValue( parms.CloudEnvironment, out profile );
            List<Instance> instances = AwsServices.DescribeEc2Instances( null, parms.Region, profile );
            foreach ( Instance instance in instances )
            {
                if ( HasMissingTags( instance.Tags, parms.MissingTags ) )
                {
                    Ec2Instance mappedInstance = Mapper.Map<Instance, Ec2Instance>( instance );
                    resultInstances.Add( mappedInstance );
                }
            }
        }
        catch ( Exception ex )
        {
            _response.Summary = (isDryRun ? "Dry run has been completed. " : "") + ex.Message;
            _encounteredFailure = true;
        }

        _response.Ec2Instances = resultInstances;
        _response.Count = resultInstances.Count;
    }

    private void ValidateRequest(Ec2Request parms)
    {
        if ( parms != null )
        {
            if ( !AwsServices.IsValidRegion( parms.Region ) )
            {
                throw new Exception( "AWS region is not valid." );
            }
            if ( !IsValidCloudEnvironment( parms.CloudEnvironment ) )
            {
                throw new Exception( "Cloud environment can not be found." );
            }
            if ( !IsValidAction( parms.Action ) )
            {
                throw new Exception( "Request action is not valid." );
            }
            if ( !SetReturnFormat( parms.ReturnFormat ) )
            {
                throw new Exception( "Valid return formats are json, xml or yaml." );
            }
            if ( !Utilities.IsValidXml( parms.Xslt ) )
            {
                throw new Exception( "XSLT is not well-formed." );
            }
        }
        else
        {
            throw new Exception( "No parameter is found in the request." );
        }
    }

    private bool IsValidCloudEnvironment(string environment)
    {
        return !string.IsNullOrWhiteSpace( environment ) && _config.AwsEnvironmentProfile.ContainsKey( environment );
    }

    public bool SetReturnFormat(string format)
    {
        bool isValid = true;
        if ( string.IsNullOrWhiteSpace( format ) )
        {
            _returnFormat = "json";
        }
        else if ( string.Equals( format, "json", StringComparison.CurrentCultureIgnoreCase ) )
        {
            _returnFormat = "json";
        }
        else if ( string.Equals( format, "xml", StringComparison.CurrentCultureIgnoreCase ) )
        {
            _returnFormat = "xml";
        }
        else if ( string.Equals( format, "yaml", StringComparison.CurrentCultureIgnoreCase ) )
        {
            _returnFormat = "yaml";
        }
        else
        {
            isValid = false;
        }
        return isValid;
    }

    public List<Filter> BuildEc2Filter(List<Ec2Filter> filters)
    {
        var resultFilters = new List<Filter>();

        foreach ( var filter in filters )
        {
            resultFilters.Add( new Filter()
            {
                Name = filter.Name,
                Values = filter.Values
            } );
        }
        return resultFilters;
    }

    public static string GetTagValue(string tagName, List<Tag> tags)
    {
        string tagValue = "";

        if ( tags != null )
        {
            foreach ( Tag tag in tags )
            {
                if ( tag.Key == tagName )
                {
                    tagValue = tag.Value;
                }
            }
        }

        return tagValue;
    }


    private void UpdateProgress(string message, StatusType status = StatusType.Any, int seqNum = -1)
    {
        _mainProgressMsg = _mainProgressMsg + Environment.NewLine + message;
        if ( status != StatusType.Any )
        {
            _result.Status = status;
        }
        if ( seqNum == 0 )
        {
            _sequenceNumber = int.MaxValue;
        }
        else
        {
            _sequenceNumber++;
        }
        OnProgress( _context, _mainProgressMsg, _result.Status, _sequenceNumber );
    }

    private bool IsValidRequestType(string requestType)
    {
        Dictionary<string, int> validRequests = new Dictionary<string, int>()
        {
            { "instance-uptime", 1 },
            { "missing-tags", 1 }
        };

        return validRequests.ContainsKey( requestType );
    }

    private bool IsValidAction(string action = "none")
    {
        Dictionary<string, int> validRequests = new Dictionary<string, int>()
        {
            { "none", 1 }
        };

        return string.IsNullOrWhiteSpace( action ) || validRequests.ContainsKey( action.ToLower() );
    }

    private bool IsValidFilters(Ec2Request request)
    {
        bool isValid = false;


        if ( request.RequestType == "instance-uptime" )
        {
            isValid = IsValidInstanceUptimeFilter( request.Uptime );
        }

        if ( request.RequestType == "missing-tags" )
        {
            if ( request.MissingTags != null )
            {
                isValid = request.MissingTags.Count > 0;
            }
        }
        return isValid;
    }

    private bool IsValidInstanceUptimeFilter(Ec2InstanceUptimeFilter filter)
    {
        bool isValid = false;
        Dictionary<string, int> operators = new Dictionary<string, int>()
        {
            {"greater-than", 1},
            {">", 1},
            {"equal", 1},
            {"==", 1},
            {"greater-or-equal", 1},
            {">=", 1},
            {"less-than", 1},
            {"<", 1},
            {"less-or-equal", 1},
            {"<=", 1}
        };

        if ( filter != null )
        {
            isValid = filter.Hours > 0 && operators.ContainsKey( filter.Operator );
        }
        return isValid;
    }

    public static bool InstanceUpFor(DateTime launchTime, uint hours, string compareOperator)
    {
        bool isTrue = false;

        double duration = DateTime.Now.Subtract( launchTime ).TotalHours;

        if ( compareOperator.Contains( "greater-than" ) || compareOperator.Contains( ">" ) )
        {
            isTrue = duration - hours > 0;
        }
        else if ( compareOperator.Contains( "equal" ) || compareOperator.Contains( "==" ) )
        {
            isTrue = (uint)duration - hours == 0;
        }
        else if ( compareOperator.Contains( "greater-or-equal" ) || compareOperator.Contains( ">=" ) )
        {
            isTrue = duration - hours >= 0;
        }
        else if ( compareOperator.Contains( "less-than" ) || compareOperator.Contains( "<" ) )
        {
            isTrue = duration - hours < 0;
        }
        else if ( compareOperator.Contains( "less-than-or-equal" ) || compareOperator.Contains( "<=" ) )
        {
            isTrue = duration - hours <= 0;
        }

        return isTrue;
    }

    bool HasMissingTags(List<Tag> tags, List<string> keys)
    {
        int foundTags = 0;

        foreach ( Tag tag in tags )
        {
            foreach ( string key in keys )
            {
                if ( String.Equals( tag.Key, key, StringComparison.CurrentCultureIgnoreCase ) && !string.IsNullOrWhiteSpace( tag.Value ) )
                {
                    foundTags++;
                }
            }
        }
        return foundTags != keys.Count;
    }
}