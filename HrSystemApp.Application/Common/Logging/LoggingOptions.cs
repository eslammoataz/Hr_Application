namespace HrSystemApp.Application.Common.Logging;

public class LoggingOptions
{
    public bool EnableWorkflowLogging         { get; set; } = true;
    public bool EnableAuthLogging             { get; set; } = true;
    public bool EnableOrgNodeLogging          { get; set; } = true;
    public bool EnableAttendanceLogging       { get; set; } = false;
    public bool EnableCommandPipelineLogging  { get; set; } = true;
    public bool EnableRequestResponseLogging  { get; set; } = false;
    public int  RequestBodyMaxLogBytes        { get; set; } = 4096;
    public int  SlowOperationThresholdMs      { get; set; } = 2000;
}
