using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

namespace Synapse.Aws.Core
{
    public partial class AwsServices
    {
        public static AWSCredentials GetAWSCredentials(string profileName = "")
        {
            if ( string.IsNullOrWhiteSpace( profileName ) )
            {
                profileName = "default";
            }
            AWSCredentials awsCredentials = null;

            CredentialProfileStoreChain chain = new CredentialProfileStoreChain();

            chain.TryGetAWSCredentials( profileName, out awsCredentials );

            return awsCredentials;
        }

        public static CredentialProfile GetCredentialProfile(string profileName = "")
        {
            if ( string.IsNullOrWhiteSpace( profileName ) )
            {
                profileName = "default";
            }
            CredentialProfile credentialProfile = null;

            CredentialProfileStoreChain chain = new CredentialProfileStoreChain();

            chain.TryGetProfile( profileName, out credentialProfile );

            return credentialProfile;
        }

        public static bool IsValidRegion(string region)
        {
            return !string.IsNullOrWhiteSpace(region) && !RegionEndpoint.GetBySystemName(region).DisplayName.Contains("Unknown");
        }
    }
}
