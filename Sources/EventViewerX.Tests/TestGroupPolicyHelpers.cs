using System;
using System.Reflection;
using EventViewerX.Helpers.ActiveDirectory;
using Xunit;

namespace EventViewerX.Tests;

public class TestGroupPolicyHelpers {
    [Fact]
    public void QueryByDistinguishedNameReturnsNullOnInvalid() {
        var result = GroupPolicyHelpers.QueryGroupPolicyByDistinguishedName("CN=NonExisting,DC=example,DC=com");
        Assert.Null(result);
    }

    [Fact]
    public void QueryInDomainByDistinguishedNameReturnsNullOnInvalid() {
        var method = typeof(GroupPolicyHelpers).GetMethod(
            "QueryGroupPolicyInDomainByDistinguishedName",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var result = method!.Invoke(null, new object?[] { "example.com", "CN=NonExisting,DC=example,DC=com" });
        Assert.Null(result);
    }
}
