using System.Collections.Generic;
using Amazon.EC2.Model;

public class AwsEc2Request
{
    public string Action { get; set; }

    public string CloudEnvironment { get; set; }

    public List<Filter> Filters { get; set; }

    public string Region { get; set; }

    public string RequestType { get; set; }

    public Ec2InstanceUptimeFilter Uptime { get; set; }

    public List<string> MissingTags { get; set; }

    public string ReturnFormat { get; set; } // JSON, XML, YAML (not case-sensitive)

    public string Xslt { get; set; }
}