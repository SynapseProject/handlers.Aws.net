using System;

public class Ec2Instance
{
    public string Architecture { get; set; }

    public string AvailabilityZone { get; set; }

    public string CloudEnvironment { get; set; }

    public string CloudEnvironmentFriendlyName { get; set; }

    public string CostCentre { get; set; }

    public string InstanceId { get; set; }

    public string InstanceState { get; set; }

    public string InstanceType { get; set; }

    public DateTime LaunchTime { get; set; }

    public string Name { get; set; }

    public string PrivateDnsName { get; set; }

    public string PrivateIpAddress { get; set; }

    public string PublicDnsName { get; set; }

    public string PublicIpAddress { get; set; }
}