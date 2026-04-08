using HrSystemApp.Application.Mappings;
using Mapster;

namespace HrSystemApp.Tests.Unit.Common;

public static class MapsterTestConfig
{
    private static bool _initialized;
    private static readonly object Sync = new();

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            TypeAdapterConfig.GlobalSettings.Scan(typeof(EmployeeMappingRegister).Assembly);
            _initialized = true;
        }
    }
}
