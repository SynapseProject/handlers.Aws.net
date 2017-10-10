using System;

public class AwsEc2Instance
{
    public string InstanceId { get; set; }
    public string Name { get; set; }
    public string Owner { get; set; }
    public string CostCentre { get; set; }
    public string InstanceType { get; set; }
    public DateTime LaunchTime { get; set; }
    public string State { get; set; }
    public string PrivateDnsName { get; set; }
    public string PrivateIpAddress { get; set; }
    public string PublicDnsName { get; set; }
    public string PublicIpAddress { get; set; }
}