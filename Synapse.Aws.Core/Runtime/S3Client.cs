﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using fs = Alphaleonis.Win32.Filesystem;

using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.IO;


namespace Synapse.Aws.Core
{
    public class S3Client
    {
        protected AmazonS3Client client;

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
            ListObjectsResponse response = (prefix == null) ? client.ListObjects( bucketName ) : client.ListObjects( bucketName, prefix );
            return response.S3Objects;
        }

        public void CopyObject(S3Object obj, string destination, string copyFrom = null, Action<string, string> logger = null)
        {
            CopyObject( obj.BucketName, obj.Key, destination, copyFrom, logger);
        }

        public void CopyObject(string bucketName, string objectKey, string destination, string copyFrom = null, Action<string, string> logger = null)
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
                    logger( "CopyObject", $"Copied [s3://{bucketName}/{objectKey}] To [{localFile}]." );
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
                        logger( "CopyObject", $" Copied [s3://{bucketName}/{objectKey}] To [{localDir}]." );
                }
            }

        }

        public void CopyBucketObjectsToLocal( string bucketName, string destination, string prefix = null, bool keepPrefixFolders = true, Action<string, string> logger = null)
        {
            List<S3Object> objects = this.GetObjects( bucketName, prefix );
            foreach ( S3Object obj in objects )
            {
                if ( keepPrefixFolders )
                    this.CopyObject( obj, destination, null, logger );
                else
                    this.CopyObject( obj, destination, prefix, logger );
            }
        }

        public void MoveBucketObjectsToLocal(string bucketName, string destination, string prefix = null, bool keepPrefixFolders = true, Action<string, string> logger = null)
        {
            List<S3Object> objects = this.GetObjects( bucketName, prefix );
            foreach ( S3Object obj in objects )
            {
                if ( keepPrefixFolders )
                    this.CopyObject( obj, destination, null );
                else
                    this.CopyObject( obj, destination, prefix );

                client.DeleteObject( bucketName, obj.Key );
                if ( logger != null )
                    logger( "MoveBucketObjectsToLocal", $" Moved [s3://{bucketName}/{obj.Key}] To [{destination}]." );
            }
        }

        public bool Exists (string bucketName, string prefix)
        {
            List<S3Object> objects = this.GetObjects( bucketName, prefix );
            return (objects.Count > 0);
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

        public string[] GetFiles(string bucketName, string prefix)
        {
            List<string> files = new List<string>();
            List<S3Object> objects = GetObjects( bucketName, prefix );
           
            foreach (S3Object obj in objects)
            {
                if ( !obj.Key.EndsWith( "/" ) )
                    files.Add( obj.Key );
            }


            return files.ToArray();
        }

        public void CopyS3ToLocal(S3FileInfo s3File, String localFile)
        {
            Stream file = s3File.OpenRead();
            localFile = localFile.Replace( "/", @"\" );
            FileStream local = fs.File.OpenWrite( localFile, Alphaleonis.Win32.Filesystem.PathFormat.FullPath );

            file.CopyTo( local );
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
