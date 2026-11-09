using System.Reflection;

namespace BranchBooking.Domain;

public static class DomainAssembly
{
    public static Assembly Assembly => typeof(DomainAssembly).Assembly;
}