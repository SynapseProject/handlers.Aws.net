using Amazon.EC2.Model;
using AutoMapper;
using Synapse.Aws.Core;
using Synapse.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Amazon.EC2;
using Newtonsoft.Json;
using StatusType = Synapse.Core.StatusType;


public class AwsEc2Handler : HandlerRuntimeBase
{
    private AwsHandlerConfig _config;
    public override object GetConfigInstance()
    {
        return new AwsHandlerConfig
        {
            SmtpServer = "xxxxxx.xxx.com",
            AwsEnvironmentProfile = new Dictionary<string, string>
            {
                { "ENV1", "AWSPROFILE1" },
                { "ENV2", "AWSPROFILE2" },
                { "ENV3", "AWSPROFILE3" }
            }
        };
    }
    private readonly ExecuteResult _result = new ExecuteResult()
    {
        Status = StatusType.None,
        BranchStatus = StatusType.None,
        Sequence = int.MaxValue
    };
    private readonly AwsEc2Response _response = new AwsEc2Response();
    private int _sequenceNumber = 0;
    private string _mainProgressMsg = "";
    private string _context = "Execute";
    private bool _encounteredFailure = false;

    public override object GetParametersInstance()
    {
        return new AwsEc2Request()
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
            MissingTags = new List<string>() { "Name", "Owner", "CostCentre" }

        };
    }

    public override IHandlerRuntime Initialize(string values)
    {
        _config = DeserializeOrNew<AwsHandlerConfig>( values );

        Mapper.Initialize( cfg =>
        {
            cfg.CreateMap<Instance, AwsEc2Instance>()
            .ForMember( d => d.CostCentre, o => o.MapFrom( s => GetTagValue( "CostCentre", s.Tags ) ) )
            .ForMember( d => d.Name, o => o.MapFrom( s => GetTagValue( "Name", s.Tags ) ) )
            .ForMember( d => d.Owner, o => o.MapFrom( s => GetTagValue( "Owner", s.Tags ) ) )
            .ForMember( d => d.State, o => o.MapFrom( s => s.State.Name ) );
        } );
        return this;
    }

    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        string message;

        try
        {
            message = "Deserializing incoming request...";
            UpdateProgress( message, StatusType.Initializing );
            string inputParameters = RemoveParameterSingleQuote( startInfo.Parameters );
            AwsEc2Request parms = DeserializeOrNew<AwsEc2Request>( inputParameters );
            // OK

            message = "Processing request...";
            UpdateProgress( message, StatusType.Running );
            if ( parms != null )
            {
                if ( IsValidRequest( parms ) )
                {
                    if ( parms.RequestType == "instance-uptime" )
                    {
                        ProcessInstanceUptimeRequest( parms, startInfo.IsDryRun );
                    }
                    if ( parms.RequestType == "missing-tags" )
                    {
                        ProcessInstanceMissingTagsRequest( parms, startInfo.IsDryRun );
                    }
                    message = "Request has been processed" + (_encounteredFailure ? " with error" : "") + ".";
                    UpdateProgress( message, _encounteredFailure ? StatusType.CompletedWithErrors : StatusType.Success );
                    _response.Summary = message;
                    // OK
                }
                else
                {
                    message = "Request contains invalid detail.";
                    UpdateProgress( message, StatusType.Failed );
                    _encounteredFailure = true;
                    _response.Summary = message;
                    // OK
                }
            }
            else
            {
                message = "No parameter is found in the request.";
                UpdateProgress( message, StatusType.Failed );
                _encounteredFailure = true;
                _response.Summary = message;
                // OK
            }
        }
        catch ( Exception ex )
        {
            message = $"Execution has been aborted due to: {ex.Message}";
            UpdateProgress( message, StatusType.Failed );
            _encounteredFailure = true;
            _response.Summary = message;
            // OK
        }


        message = "Serializing response...";
        UpdateProgress( message );
        _result.ExitData = JsonConvert.SerializeObject( _response );
        _result.ExitCode = _encounteredFailure ? -1 : 0;

        message = startInfo.IsDryRun ? "Dry run execution is completed." : "Execution is completed.";
        UpdateProgress( message, StatusType.Any, 0 );
        return _result;
        // OK
    }

    private void ProcessInstanceUptimeRequest(AwsEc2Request parms, bool isDryRun = false)
    {
        List<AwsEc2Instance> resultInstances = new List<AwsEc2Instance>();

        try
        {
            string profile;
            _config.AwsEnvironmentProfile.TryGetValue( parms.CloudEnvironment, out profile );
            List<Instance> instances = AwsServices.DescribeEc2Instances( null, parms.Region, profile );
            foreach ( Instance instance in instances )
            {
                if ( instance.State.Name == InstanceStateName.Running && InstanceUpFor( instance.LaunchTime, parms.Uptime.Hours, parms.Uptime.Operator ) )
                {
                    AwsEc2Instance mappedInstance = Mapper.Map<Instance, AwsEc2Instance>( instance );
                    resultInstances.Add( mappedInstance );
                }
            }
        }
        catch ( Exception ex )
        {
            _response.Summary = (isDryRun ? "Dry run has been completed. " : "") + ex.Message;
            _encounteredFailure = true;
        }

        _response.Instances = resultInstances;
        _response.InstanceCount = resultInstances.Count;
    }

    private void ProcessInstanceMissingTagsRequest(AwsEc2Request parms, bool isDryRun = false)
    {
        List<AwsEc2Instance> resultInstances = new List<AwsEc2Instance>();

        try
        {
            string profile;
            _config.AwsEnvironmentProfile.TryGetValue( parms.CloudEnvironment, out profile );
            List<Instance> instances = AwsServices.DescribeEc2Instances( null, parms.Region, profile );
            foreach ( Instance instance in instances )
            {
                if ( HasMissingTags(instance.Tags, parms.MissingTags) )
                {
                    AwsEc2Instance mappedInstance = Mapper.Map<Instance, AwsEc2Instance>( instance );
                    resultInstances.Add( mappedInstance );
                }
            }
        }
        catch ( Exception ex )
        {
            _response.Summary = (isDryRun ? "Dry run has been completed. " : "") + ex.Message;
            _encounteredFailure = true;
        }

        _response.Instances = resultInstances;
        _response.InstanceCount = resultInstances.Count;
    }

    private bool IsValidRequest(AwsEc2Request parms)
    {
        bool isValid = true;
        if ( parms != null )
        {
            if ( parms.Region.ToLower() != "all" && !AwsServices.IsValidRegion( parms.Region ) )
            {
                isValid = false;
                UpdateProgress( "AWS region is not valid.", StatusType.Failed, 0 );
            }

            if ( !_config.AwsEnvironmentProfile.ContainsKey( parms.CloudEnvironment ) )
            {
                isValid = false;
                UpdateProgress( "Cloud environment specified can not be found.", StatusType.Failed, 0 );
            }

            if ( !IsValidRequestType( parms.RequestType ) )
            {
                isValid = false;
                UpdateProgress( "Request type is not valid.", StatusType.Failed, 0 );
            }

            if ( !IsValidAction( parms.Action ) )
            {
                isValid = false;
                UpdateProgress( "Request action is not valid.", StatusType.Failed, 0 );
            }

            if ( !IsValidFilters( parms ) )
            {
                isValid = false;
                UpdateProgress( "Request filter is not valid.", StatusType.Failed, 0 );
            }
        }
        return isValid;
    }

    public List<Filter> BuildEc2Filter(List<AwsEc2Filter> filters)
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

    public static List<AwsEc2Instance> MapEc2Instances(IEnumerable<Instance> instances)
    {
        List<AwsEc2Instance> mappedInstances = new List<AwsEc2Instance>();

        foreach ( var instance in instances )
        {
            mappedInstances.Add( new AwsEc2Instance()
            {
                InstanceId = instance.InstanceId,
                Name = GetTagValue( "Name", instance.Tags ),
                Owner = GetTagValue( "Owner", instance.Tags ),
                CostCentre = GetTagValue( "CostCentre", instance.Tags ),
                InstanceType = instance.InstanceType,
                LaunchTime = instance.LaunchTime,
                State = instance.State.Name,
                PrivateDnsName = instance.PrivateDnsName,
                PrivateIpAddress = instance.PrivateIpAddress,
                PublicDnsName = instance.PublicDnsName,
                PublicIpAddress = instance.PublicIpAddress
            } );
        }

        return mappedInstances;
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

    private static string RemoveParameterSingleQuote(string input)
    {
        string output = "";
        if ( !string.IsNullOrWhiteSpace( input ) )
        {
            Regex pattern = new Regex( "'(\r\n|\r|\n|$)" );
            output = input.Replace( ": '", ": " );
            output = pattern.Replace( output, Environment.NewLine );
        }
        return output;
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
            { "none", 1 },
            { "notify", 1 }
        };

        return string.IsNullOrWhiteSpace( action ) || validRequests.ContainsKey( action );
    }

    private bool IsValidFilters(AwsEc2Request request)
    {
        bool isValid = false;


        if ( request.RequestType == "instance-uptime" )
        {
            isValid = IsValidInstanceUptimeFilter( request.Uptime );
        }

        if (request.RequestType == "missing-tags")
        {
            if (request.MissingTags != null)
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
            foreach (string key in keys)
            {
                if ( tag.Key.ToLower() == key.ToLower() && !string.IsNullOrWhiteSpace( tag.Value ) )
                {
                    foundTags++;
                }
            }
        }
        return foundTags != keys.Count;
    }
}