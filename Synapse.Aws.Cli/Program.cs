using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Amazon;
using Synapse.Aws.Core;

namespace Synapse.Aws.Cli
{
    public class Program
    {
        public static void Main(string[] args)
        {
//            String accessKey = "xxxxxxxxxxxxxxxxxxxx";
//            String secretKey = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
            RegionEndpoint endpoint = RegionEndpoint.EUWest1;
//            S3Client client = new S3Client( accessKey, secretKey, endpoint );
            S3Client client = new S3Client( endpoint );

            client.CopyBucketObjectsToLocal( "wagug0-test", @"\\?\UNC\localhost\C$\Temp\Destination", "DevA/", false );

            Console.WriteLine( "Press <ENTER> To Continue..." );
            Console.ReadLine();
        }
    }
}
