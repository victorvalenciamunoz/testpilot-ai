namespace TestPilot.Core.Models;

public record ClassInfo(string FullName, string FilePath, IReadOnlyList<MethodInfo> PublicMethods);
