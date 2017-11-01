using Amazon;
using Amazon.EC2.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.EC2;

namespace Synapse.Aws.Core
{
    public partial class AwsServices
    {
        public static List<Instance> DescribeEc2Instances(List<Filter> filters, string regionName, string profileName)
        {
            AWSCredentials creds = GetAWSCredentials( profileName );

            if ( creds == null )
            {
                throw new Exception( "AWS credentials are not specified" );
            }

            RegionEndpoint endpoint = RegionEndpoint.GetBySystemName( regionName );
            if ( endpoint.DisplayName.Contains( "Unknown" ) )
            {
                throw new Exception( "AWS region endpoint is not valid." );
            }

            List<Instance> instances = new List<Instance>();

            try
            {
                using ( AmazonEC2Client client = new AmazonEC2Client( creds, endpoint ) )
                {
                    DescribeInstancesRequest req = new DescribeInstancesRequest { Filters = filters };
                    do
                    {
                        DescribeInstancesResponse resp = client.DescribeInstances( req );
                        if ( resp != null )
                        {
                            instances.AddRange( resp.Reservations.SelectMany( reservation => reservation.Instances ) );
                            req.NextToken = resp.NextToken;
                        }
                    } while ( !string.IsNullOrWhiteSpace( req.NextToken ) );
                }

            }
            catch ( Exception ex )
            {
                throw new Exception( $"Encountered exception while describing EC2 instances: {ex.Message}" );
            }

            return instances;
        }
    }
}