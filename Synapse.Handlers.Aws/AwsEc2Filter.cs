using System.Collections.Generic;

public class AwsEc2Filter
{
    public string Name { get; set; }
    public List<string> Values { get; set; }
    public string Operator { get; set; }
}