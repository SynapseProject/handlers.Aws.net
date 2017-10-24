using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

using fs = Alphaleonis.Win32.Filesystem;

using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.IO;
using Amazon.S3.Transfer;


namespace Synapse.Aws.Core
{
    public class S3Client
    {
        protected AmazonS3Client client;
        protected TransferUtility transferUtil;

        #region Constructors
        public S3Client()
        {
            client = new AmazonS3Client();
        }

        public S3Client(RegionEndpoint endpoint)
        {
            client = new AmazonS3Client( endpoint );
        }

        public S3Client(AWSCredentials creds)
        {
            client = new AmazonS3Client( creds );
        }

        public S3Client(AWSCredentials creds, RegionEndpoint endpoint)
        {
            client = new AmazonS3Client( creds, endpoint );
        }

        public S3Client( string accessKey, string secretAccessKey )
        {
            client = new AmazonS3Client( accessKey, secretAccessKey );
        }

        public S3Client(string accessKey, string secretAccessKey, RegionEndpoint endpoint)
        {
            client = new AmazonS3Client( accessKey, secretAccessKey, endpoint );
        }

        ~S3Client()
        {
            client.Dispose();
        }
        #endregion Constructors

        #region Public Methods
        public List<S3Bucket> GetBuckets()
        {
            ListBucketsResponse response = client.ListBuckets();
            return response.Buckets;
        }

        public List<S3Object> GetObjects(string bucketName, string prefix = null)
        {
            ListObjectsV2Request request = new ListObjectsV2Request();
            request.BucketName = bucketName;
            if ( prefix != null )
                request.Prefix = prefix;

            ListObjectsV2Response response = client.ListObjectsV2( request );
            return response.S3Objects;
        }

        public void CopyObject(S3Object obj, string destinationBucket, string destinationPrefix, string copyFrom = null, Action<string, string> logger = null)
        {
            CopyObject( obj.BucketName, obj.Key, destinationBucket, destinationPrefix, copyFrom, logger );
        }

        public void CopyObject(string sourceBucket, string sourceKey, string destinationBucket, string destinationPrefix, string copyFrom = null, Action<string, string> logger = null)
        {
            string destinationKey = sourceKey;
            if ( !String.IsNullOrWhiteSpace( copyFrom ) )
                destinationKey = destinationPrefix + sourceKey.Replace( copyFrom, "" );

            client.CopyObject( sourceBucket, sourceKey, destinationBucket, destinationKey );
            if ( logger != null )
                logger( sourceBucket, $"Copied [s3://{sourceBucket}/{sourceKey}] To [s3://{destinationBucket}/{destinationKey}]." );

        }
        public void CopyObjectToLocal(S3Object obj, string destination, string copyFrom = null, Action<string, string> logger = null)
        {
            CopyObjectToLocal( obj.BucketName, obj.Key, destination, copyFrom, logger);
        }

        public void CopyObjectToLocal(string bucketName, string objectKey, string destination, string copyFrom = null, Action<string, string> logger = null)
        {
            FileSystemType type = FileSystemType.File;
            if ( objectKey.EndsWith( "/" ) )
                type = FileSystemType.Directory;

            if ( type == FileSystemType.File )
            {
                S3FileInfo file = new S3FileInfo( client, bucketName, objectKey );
                String localFile = fs.Path.Combine( destination, objectKey );
                if ( copyFrom != null )
                {
                    if ( !copyFrom.EndsWith( "/" ) )
                        copyFrom += "/";
                    localFile = localFile.Replace( copyFrom, "" );
                }

                if ( fs.File.Exists( localFile ) )
                    fs.File.Delete( localFile );
                //file.CopyToLocal( localFile );
                CopyS3ToLocal( file, localFile );
                if ( logger != null )
                    logger( bucketName, $"Copied [s3://{bucketName}/{objectKey}] To [{localFile}]." );
            }
            else if ( type == FileSystemType.Directory )
            {
                S3DirectoryInfo dir = new S3DirectoryInfo( client, bucketName, objectKey );
                String localDir = fs.Path.Combine( destination, objectKey );
                if ( copyFrom != null )
                {
                    if ( !copyFrom.EndsWith( "/" ) )
                        copyFrom += "/";
                    localDir = localDir.Replace( copyFrom, "" );
                }
                if ( !fs.Directory.Exists( localDir ) )
                {
                    fs.Directory.CreateDirectory( localDir );
                    if ( logger != null )
                        logger( bucketName, $" Copied [s3://{bucketName}/{objectKey}] To [{localDir}]." );
                }
            }
        }

        public void CreateS3Directory(string bucket, string key, Action<string, string> logger = null)
        {
            if ( transferUtil == null )
                transferUtil = new TransferUtility( client );

            if ( !key.EndsWith( "/" ) )
                key = key + "/";

            key = key.Replace( '\\', '/' );

            transferUtil.Upload( Stream.Null, bucket, key );
            if ( logger != null )
                logger( bucket, $"Created Directory [s3://{bucket}/{key}]." );
        }

        public void UploadToBucket(string sourceFile, string bucket, string prefix, string copyFrom = null, Action<string, string> logger = null)
        {
            if ( transferUtil == null )
                transferUtil = new TransferUtility( client );

            String key = prefix + "/" + sourceFile;
            if ( copyFrom != null )
                key = key.Replace( copyFrom, "" );

            key = key.Replace( '\\', '/' );
            key = Regex.Replace( key, "//+", "/" );

            transferUtil.Upload( sourceFile, bucket, key );

            if ( logger != null )
                logger( bucket, $"Copied [{sourceFile}] to [s3://{bucket}/{key}]" );
        }

        public void CopyBucketObjects( string sourceBucket, string sourcePrefix, string destinationBucket, string destinationPrefix, bool keepPrefixFolders = true, Action<string, string> logger = null)
        {
            List<S3Object> objects = this.GetObjects( sourceBucket, sourcePrefix );
            foreach ( S3Object obj in objects )
            {
                if ( keepPrefixFolders )
                    this.CopyObject( obj, destinationBucket, destinationPrefix, null, logger );
                else
                    this.CopyObject( obj, destinationBucket, destinationPrefix, sourcePrefix, logger );
            }
        }

        public void CopyBucketObjectsToLocal( string bucketName, string destination, string prefix = null, bool keepPrefixFolders = true, Action<string, string> logger = null)
        {
            List<S3Object> objects = this.GetObjects( bucketName, prefix );
            foreach ( S3Object obj in objects )
            {
                if ( keepPrefixFolders )
                    this.CopyObjectToLocal( obj, destination, null, logger );
                else
                    this.CopyObjectToLocal( obj, destination, prefix, logger );
            }
        }

        public void UploadFilesToBucket(string sourceDir, string bucket, string prefix, Action<string, string> logger = null)
        {
            string[] dirs = fs.Directory.GetDirectories( sourceDir, "*", SearchOption.AllDirectories );
            foreach ( string dir in dirs )
            {
                string key = $"{prefix}/{dir.Replace( sourceDir + "\\", "" )}";
                this.CreateS3Directory( bucket, key, logger );
            }

            string[] files = fs.Directory.GetFiles( sourceDir, "*", SearchOption.AllDirectories );
            foreach ( string file in files )
                this.UploadToBucket( file, bucket, prefix, sourceDir, logger );
        }

        public void MoveFilesToBucket(string sourceDir, string bucket, string prefix, Action<string, string> logger = null)
        {
            string[] files = fs.Directory.GetFiles( sourceDir, "*", SearchOption.AllDirectories );
            foreach ( string file in files )
            {
                this.UploadToBucket( file, bucket, prefix, sourceDir, logger );
                fs.File.Delete( file );
                if (logger != null)
                    logger( bucket, $"Deleted [{file}]." );
            }

            string[] dirs = fs.Directory.GetDirectories( sourceDir, "*", SearchOption.AllDirectories );
            foreach ( string dir in dirs )
            {
                string key = $"{prefix}/{dir.Replace( sourceDir + "\\", "" )}";
                this.CreateS3Directory( bucket, key, logger );
                if ( fs.Directory.Exists( dir ) )
                    fs.Directory.Delete( dir, true );
                if ( logger != null )
                    logger( bucket, $"Deleted [{dir}]." );
            }

        }

        public void MoveBucketObjects(string sourceBucket, string sourcePrefix, string destinationBucket, string destinationPrefix, bool keepPrefixFolders = true, bool keepSourceFolder = false, Action<string, string> logger = null)
        {
            List<S3Object> objects = this.GetObjects( sourceBucket, sourcePrefix );
            foreach ( S3Object obj in objects )
            {
                if ( keepPrefixFolders )
                    this.CopyObject( obj, destinationBucket, destinationPrefix, null, logger );
                else
                    this.CopyObject( obj, destinationBucket, destinationPrefix, sourcePrefix, logger );

                // Check If Main Source Directory.  If so, don't delete it.
                bool deleteObject = true;
                if ( sourcePrefix != null )
                    if ( (sourcePrefix.Replace( "/", "" ) == obj.Key.Replace( "/", "" )) && keepSourceFolder )
                        deleteObject = false;

                if ( deleteObject )
                {
                    client.DeleteObject( obj.BucketName, obj.Key );
                    if (logger != null)
                        logger( obj.BucketName, $"Deleted [s3://{obj.BucketName}/{obj.Key}]" );
                }
            }
        }

        public void MoveBucketObjectsToLocal(string bucketName, string destination, string prefix = null, bool keepPrefixFolders = true, Action<string, string> logger = null)
        {
            List<S3Object> objects = this.GetObjects( bucketName, prefix );
            foreach ( S3Object obj in objects )
            {
                if ( keepPrefixFolders )
                    this.CopyObjectToLocal( obj, destination, null, logger );
                else
                    this.CopyObjectToLocal( obj, destination, prefix, logger );

                // Check If Main Source Directory.  If so, don't delete it.
                bool deleteObject = true;
                if ( prefix != null )
                    if ( prefix.Replace( "/", "" ) == obj.Key.Replace( "/", "" ) )
                        deleteObject = false;

                if ( deleteObject )
                {
                    client.DeleteObject( obj.BucketName, obj.Key );
                    if ( logger != null )
                        logger( obj.BucketName, $"Deleted [s3://{obj.BucketName}/{obj.Key}]" );
                }
            }
        }

        public void DeleteBucketObjects(string bucketName, string prefix, Action<string, string> logger = null)
        {
            List<S3Object> objects = this.GetObjects( bucketName, prefix );
            foreach ( S3Object obj in objects )
            {

                // Check If Main Source Directory.  If so, don't delete it.
                bool deleteObject = true;
                if ( prefix != null )
                    if ( prefix.Replace( "/", "" ) == obj.Key.Replace( "/", "" ) )
                        deleteObject = false;

                if ( deleteObject )
                {
                    client.DeleteObject( obj.BucketName, obj.Key );
                    if ( logger != null )
                        logger( obj.BucketName, $"Deleted [s3://{obj.BucketName}/{obj.Key}]" );
                }
            }
        }

        public void DeleteObject(string bucketName, string key, Action<string, string> logger = null)
        {
            client.DeleteObject( bucketName, key );
            if ( logger != null )
                logger( bucketName, $"Deleted [s3://{bucketName}/{key}]" );
        }

        public void DeleteBucket(string bucketName)
        {
            client.DeleteBucket( bucketName );
        }

        public bool Exists (string bucketName, string prefix)
        {
            List<S3Object> objects = this.GetObjects( bucketName, prefix );
            return (objects.Count > 0);
        }

        public Stream GetObjectStream(string bucketName, string objectKey, S3FileMode fileMode = S3FileMode.Open, S3FileAccess fileAccess = S3FileAccess.Read)
        {
            Stream stream = null;
            S3FileInfo file = new S3FileInfo( client, bucketName, objectKey );
            bool fileExists = file.Exists;

            if (fileMode == S3FileMode.Create || fileMode == S3FileMode.CreateNew || fileMode == S3FileMode.Truncate)
            {
                if (fileExists)
                {
                    if ( fileMode == S3FileMode.CreateNew )
                        throw new Exception( $"Object [s3://{bucketName}/{objectKey}] Already Exists." );
                    file.Delete();
                }
                else if (fileMode == S3FileMode.Truncate)
                    throw new Exception( $"Object [s3://{bucketName}/{objectKey}] Does Not Exist." );

                stream = file.Create();
            }
            else if (fileMode == S3FileMode.Open || fileMode == S3FileMode.OpenOrCreate)
            {
                if (!fileExists)
                {
                    if (fileMode == S3FileMode.Open)
                        throw new Exception( $"Object [s3://{bucketName}/{objectKey}] Does Not Exist." );
                    stream = file.Create();
                    stream.Close();
                }

                if ( fileAccess == S3FileAccess.Read )
                    stream = file.OpenRead();
                else
                    stream = file.OpenWrite();
            }

            return stream;
        }

        public string[] ReadAllLines(string bucketName, string objectKey)
        {
            List<String> lines = new List<string>();

            S3FileInfo file = new S3FileInfo( client, bucketName, objectKey );
            if (file.Exists)
            {
                StreamReader reader = file.OpenText();
                string line = null;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add( line );
                }
            }

            return lines.ToArray();
        }

        public string[] GetFiles(string bucketName, string prefix, bool returnFullPath = true)
        {
            List<string> files = new List<string>();
            List<S3Object> objects = GetObjects( bucketName, prefix );
           
            foreach (S3Object obj in objects)
            {
                if ( !obj.Key.EndsWith( "/" ) )
                {
                    if ( returnFullPath )
                        files.Add( $"s3://{obj.BucketName}/{obj.Key}" );
                    else
                        files.Add( obj.Key );
                }
            }


            return files.ToArray();
        }

        public void CopyS3ToLocal(S3FileInfo s3File, String localFile)
        {
            string localDir = fs.Path.GetDirectoryName( localFile );
            if ( !fs.Directory.Exists( localDir ) )
                fs.Directory.CreateDirectory( localDir );

            Stream file = s3File.OpenRead();
            localFile = localFile.Replace( "/", @"\" );
            FileStream local = fs.File.OpenWrite( localFile, Alphaleonis.Win32.Filesystem.PathFormat.FullPath );

            file.CopyTo( local );
            local.Close();
        }

        public void CopyLocalToS3(String localFile, S3FileInfo s3File)
        {
            FileStream local = fs.File.OpenRead( localFile, Alphaleonis.Win32.Filesystem.PathFormat.FullPath );
            Stream file = s3File.OpenWrite();
            local.CopyTo( file );
        }

        #endregion Public Methods
    }
}
