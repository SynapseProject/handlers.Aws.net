using System.Collections.Generic;

public class AwsHandlerConfig
{
    public string SmtpServer { get; set; }
    public Dictionary<string, string> AwsEnvironmentProfile { get; set; }
}