using System.Collections.Generic;

public class AwsEc2Response
{
    public string Summary { get; set; }
    public int InstanceCount { get; set; }
    public List<AwsEc2Instance> Instances { get; set; }
}