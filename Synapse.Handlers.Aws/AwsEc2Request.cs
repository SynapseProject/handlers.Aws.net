using System.Collections.Generic;

public class AwsEc2Request
{
    public string Region { get; set; }
    public string CloudEnvironment { get; set; }
    public string RequestType { get; set; }
    public string Action { get; set; }
    public Ec2InstanceUptimeFilter Uptime { get; set; }
    public List<string> MissingTags { get; set; }
}