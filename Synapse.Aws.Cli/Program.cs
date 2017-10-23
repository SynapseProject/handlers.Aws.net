using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using Amazon;
using Amazon.S3.Model;
using Amazon.S3.IO;
using Synapse.Aws.Core;

namespace Synapse.Aws.Cli
{
    public class Program
    {
        public static void Main(string[] args)
        {
            RegionEndpoint endpoint = RegionEndpoint.EUWest1;
            S3Client client = new S3Client( endpoint );

            Console.WriteLine( "Press <ENTER> To Continue..." );
            Console.ReadLine();
        }
    }
}
