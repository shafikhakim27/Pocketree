# Test Framework Comparison Guide

This project uses **three testing frameworks** to demonstrate different testing approaches in .NET 9:

## ?? Framework Overview

| Framework | Version | Attributes | Assertions | Popularity |
|-----------|---------|-----------|------------|------------|
| **xUnit** | 2.9.3 | `[Fact]`, `[Theory]` | `Assert.Equal()` | ????? (Most popular) |
| **MSTest** | 3.8.2 | `[TestMethod]`, `[DataRow]` | `Assert.AreEqual()` | ???? |
| **NUnit** | 4.3.1 | `[Test]`, `[TestCase]` | `Assert.That()` | ???? |

---

## ?? Test Structure Comparison

### xUnit (Recommended for .NET Core)
```csharp
[Fact]  // Simple test
public void Test_Scenario_Expected()
{
    // Arrange
    var value = 100;
    
    // Act
    var result = Calculate(value);
    
    // Assert
    Assert.Equal(200, result);
}

[Theory]  // Data-driven test
[InlineData("Easy", 100)]
[InlineData("Normal", 200)]
public void Test_MultipleInputs(string difficulty, int expected)
{
    Assert.Equal(expected, GetReward(difficulty));
}
```

**Pros:**
- Modern, designed for .NET Core
- No `[TestClass]` required
- Clean syntax
- Used by .NET team

**Cons:**
- Less mature than NUnit
- Different assertion style

---

### MSTest (Microsoft's Framework)
```csharp
[TestClass]  // Required class attribute
public class MyTests
{
    [TestMethod]  // Simple test
    public void Test_Scenario_Expected()
    {
        // Arrange
        var value = 100;
        
        // Act
        var result = Calculate(value);
        
        // Assert
        Assert.AreEqual(200, result);
    }

    [DataTestMethod]  // Data-driven test
    [DataRow("Easy", 100)]
    [DataRow("Normal", 200)]
    public void Test_MultipleInputs(string difficulty, int expected)
    {
        Assert.AreEqual(expected, GetReward(difficulty));
    }
}
```

**Pros:**
- Built-in Visual Studio integration
- Official Microsoft support
- Enterprise-friendly

**Cons:**
- Requires `[TestClass]` attribute
- Older syntax style

---

### NUnit (Classic Framework)
```csharp
[TestFixture]  // Required class attribute
public class MyTests
{
    [Test]  // Simple test
    public void Test_Scenario_Expected()
    {
        // Arrange
        var value = 100;
        
        // Act
        var result = Calculate(value);
        
        // Assert
        Assert.That(result, Is.EqualTo(200));
    }

    [TestCase("Easy", 100)]  // Data-driven test
    [TestCase("Normal", 200)]
    public void Test_MultipleInputs(string difficulty, int expected)
    {
        Assert.That(GetReward(difficulty), Is.EqualTo(expected));
    }
}
```

**Pros:**
- Most mature framework
- Powerful constraint model (`Is.EqualTo`, `Does.Contain`)
- Extensive features

**Cons:**
- More verbose
- Requires `[TestFixture]` attribute

---

## ?? Common Assertions Comparison

| Operation | xUnit | MSTest | NUnit |
|-----------|-------|--------|-------|
| **Equality** | `Assert.Equal(expected, actual)` | `Assert.AreEqual(expected, actual)` | `Assert.That(actual, Is.EqualTo(expected))` |
| **Not Null** | `Assert.NotNull(obj)` | `Assert.IsNotNull(obj)` | `Assert.That(obj, Is.Not.Null)` |
| **True/False** | `Assert.True(condition)` | `Assert.IsTrue(condition)` | `Assert.That(condition, Is.True)` |
| **Empty Collection** | `Assert.Empty(collection)` | `Assert.AreEqual(0, collection.Count)` | `Assert.That(collection, Is.Empty)` |
| **Contains** | `Assert.Contains(item, collection)` | `CollectionAssert.Contains(collection, item)` | `Assert.That(collection, Does.Contain(item))` |

---

## ?? Test Project Structure

```
Pocketree.Api.Tests/
??? UnitTest1.cs                          # xUnit tests (53 tests)
??? SharedLibraryTests.cs                 # xUnit shared library tests
??? MSTest/
?   ??? DbContextTests_MSTest.cs         # MSTest DbContext tests
?   ??? SharedLibraryTests_MSTest.cs     # MSTest shared library tests
??? NUnit/
    ??? DbContextTests_NUnit.cs          # NUnit DbContext tests
    ??? SharedLibraryTests_NUnit.cs      # NUnit shared library tests
```

---

## ?? Running Tests by Framework

### Visual Studio Test Explorer
1. Open **Test** ? **Test Explorer**
2. Group by **Traits** or **Class** to see frameworks separately
3. Run all or filter by framework name

### Command Line

**Run all tests:**
```bash
dotnet test
```

**Run only xUnit tests:**
```bash
dotnet test --filter "FullyQualifiedName!~MSTest&FullyQualifiedName!~NUnit"
```

**Run only MSTest tests:**
```bash
dotnet test --filter "FullyQualifiedName~MSTest"
```

**Run only NUnit tests:**
```bash
dotnet test --filter "FullyQualifiedName~NUnit"
```

---

## ?? Expected Test Results

| Framework | Test Count | Status |
|-----------|-----------|---------|
| **xUnit** | 53 tests | ? All Passing |
| **MSTest** | 40+ tests | ? All Passing |
| **NUnit** | 40+ tests | ? All Passing |
| **Total** | 130+ tests | ? All Passing |

---

## ?? Which Framework Should I Use?

### Choose **xUnit** if:
- ? Working on new .NET Core/.NET 9 projects (like Pocketree)
- ? Want modern, clean syntax
- ? Following .NET team best practices

### Choose **MSTest** if:
- ? Enterprise environment with Microsoft stack
- ? Need tight Visual Studio integration
- ? Corporate standards require it

### Choose **NUnit** if:
- ? Migrating from older .NET Framework projects
- ? Need advanced constraint model
- ? Team is already familiar with it

---

## ?? Recommendation for Pocketree

**Primary Framework: xUnit**
- All new tests should use xUnit
- It's the standard for modern .NET projects
- Better suited for .NET 9

**MSTest & NUnit: Learning/Compatibility**
- Keep for demonstration purposes
- Use for learning different testing styles
- Helpful when joining teams using different frameworks

---

## ?? Additional Resources

- [xUnit Documentation](https://xunit.net/)
- [MSTest Documentation](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-mstest)
- [NUnit Documentation](https://docs.nunit.org/)

---

**Last Updated:** January 2026  
**Project:** Pocketree API (.NET 9)
