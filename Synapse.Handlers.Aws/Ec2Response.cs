using System.Collections.Generic;

public class Ec2Response
{
    public int Count { get; set; }
    public List<Ec2Instance> Ec2Instances { get; set; }
    public int ExitCode { get; set; }
    public string Summary { get; set; }
}