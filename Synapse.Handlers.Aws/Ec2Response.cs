using System.Collections.Generic;

public class Ec2Response
{
    public string Summary { get; set; }
    public int Count { get; set; }
    public List<Ec2Instance> Ec2Instances { get; set; }
}