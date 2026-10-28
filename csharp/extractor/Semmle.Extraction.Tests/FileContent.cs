using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Semmle.Extraction.CSharp.DependencyFetching;
using Semmle.Util.Logging;

namespace Semmle.Extraction.Tests
{
    internal class UnsafeFileReaderStub : IUnsafeFileReader
    {
        private readonly IReadOnlyDictionary<string, string> contents;
        public int OpenCount { get; private set; }
        public MemoryStream? LastStream { get; private set; }

        public UnsafeFileReaderStub(IReadOnlyDictionary<string, string> contents)
        {
            this.contents = contents;
        }

        public Stream OpenRead(string file)
        {
            OpenCount++;
            LastStream = new MemoryStream(Encoding.UTF8.GetBytes(contents[file]));
            return LastStream;
        }
    }

    internal class TestFileContent : FileContent
    {
        public TestFileContent(IEnumerable<string> lines, string file = "test1.csproj") : base(new LoggerStub(),
            new[] { file },
            new UnsafeFileReaderStub(new Dictionary<string, string> { [file] = string.Join(Environment.NewLine, lines) }))
        { }
    }

    public class FileContentTests
    {
        [Fact]
        public void TestFileContent1()
        {
            // Setup
            var lines = new List<string>()
            {
                "<Project Sdk=\"Microsoft.NET.Sdk\">",
                "<PackageReference Include=\"DotNetAnalyzers.DocumentationAnalyzers\" Version=\"1.0.0-beta.59\" PrivateAssets=\"all\" />",
                "<PackageReference Version=\"7.0.0\" Include=\"Microsoft.CodeAnalysis.NetAnalyzers\" PrivateAssets=\"all\" />",
                "<PackageReference Include=\"StyleCop.Analyzers\" Version=\"1.2.0-beta.406\"></PackageReference>",
                "<!-- <PackageReference Include=\"NUnit\" Version=\"3.10.1\" /> -->",
                "<FrameworkReference Include=\"My.Framework\"/>",
                "</Project>"
            };
            var fileContent = new TestFileContent(lines);

            // Execute
            var allPackages = fileContent.AllPackages;
            var useAspNetDlls = fileContent.UseAspNetCoreDlls;

            // Verify
            Assert.False(useAspNetDlls);
            Assert.Equal(3, allPackages.Count);
            Assert.Contains(new PackageReference("DotNetAnalyzers.DocumentationAnalyzers".ToLowerInvariant(), PackageReferenceSource.SdkCsProj), allPackages);
            Assert.Contains(new PackageReference("Microsoft.CodeAnalysis.NetAnalyzers".ToLowerInvariant(), PackageReferenceSource.SdkCsProj), allPackages);
            Assert.Contains(new PackageReference("StyleCop.Analyzers".ToLowerInvariant(), PackageReferenceSource.SdkCsProj), allPackages);
        }

        [Fact]
        public void TestFileContent2()
        {
            // Setup
            var lines = new List<string>()
                {
                    "<Project Sdk=\"Microsoft.NET.Sdk.Web\">",
                    "<FrameworkReference Include=\"My.Framework\"/>",
                    "<PackageReference Version=\"7.0.0\" Include=\"Microsoft.CodeAnalysis.NetAnalyzers\" PrivateAssets=\"all\" />",
                    "<PackageReference Include=\"StyleCop.Analyzers\" Version=\"1.2.0-beta.406\" />",
                    "</Project>"
                };
            var fileContent = new TestFileContent(lines);

            // Execute
            var useAspNetDlls = fileContent.UseAspNetCoreDlls;
            var allPackages = fileContent.AllPackages;

            // Verify
            Assert.True(useAspNetDlls);
            Assert.Equal(2, allPackages.Count);
            Assert.Contains(new PackageReference("Microsoft.CodeAnalysis.NetAnalyzers".ToLowerInvariant(), PackageReferenceSource.SdkCsProj), allPackages);
            Assert.Contains(new PackageReference("StyleCop.Analyzers".ToLowerInvariant(), PackageReferenceSource.SdkCsProj), allPackages);
        }

        private static void CsProjSettingsTest(string line, bool expected, Func<FileContent, bool> func)
        {
            // Setup
            var lines = new List<string>()
                {
                    line
                };
            var fileContent = new TestFileContent(lines);

            // Execute
            var actual = func(fileContent);

            // Verify
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestFileContent_ImplicitUsings0()
        {
            CsProjSettingsTest("<ImplicitUsings>false</ImplicitUsings>", false, fc => fc.UseImplicitUsings);
        }

        [Fact]
        public void TestFileContent_ImplicitUsings1()
        {
            CsProjSettingsTest("<ImplicitUsings>true</ImplicitUsings>", true, fc => fc.UseImplicitUsings);
        }

        [Fact]
        public void TestFileContent_ImplicitUsings2()
        {
            CsProjSettingsTest("<ImplicitUsings>enable</ImplicitUsings>", true, fc => fc.UseImplicitUsings);
        }

        [Fact]
        public void TestFileContent_UseWpf0()
        {
            CsProjSettingsTest("<UseWPF>false</UseWPF>", false, fc => fc.UseWpf);
        }

        [Fact]
        public void TestFileContent_UseWpf1()
        {
            CsProjSettingsTest("<UseWPF>true</UseWPF>", true, fc => fc.UseWpf);
        }

        [Fact]
        public void TestFileContent_UseWindowsForms0()
        {
            CsProjSettingsTest("<UseWindowsForms>false</UseWindowsForms>", false, fc => fc.UseWindowsForms);
        }

        [Fact]
        public void TestFileContent_UseWindowsForms1()
        {
            CsProjSettingsTest("<UseWindowsForms>true</UseWindowsForms>", true, fc => fc.UseWindowsForms);
        }

        [Fact]
        public void TestFileContent_ImplicitUsingsAdditional()
        {
            // Setup
            var lines = new List<string>()
                {
                    "<Project>",
                    "<Using Include=\"Ns0.Ns1\" />",
                    "<Using Include=\"Ns2\" />",
                    "<Using Remove=\"Ns3\" />",
                    "</Project>"
                };
            var fileContent = new TestFileContent(lines);

            // Execute
            var customImplicitUsings = fileContent.CustomImplicitUsings;

            // Verify
            Assert.Equal(2, customImplicitUsings.Count);
            Assert.Contains("Ns0.Ns1", customImplicitUsings);
            Assert.Contains("Ns2", customImplicitUsings);
        }

        [Fact]
        public void TestFileContent_LegacyProjectStructure()
        {
            // Setup
            var input =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="12.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
              <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets" />
            </Project>
            """;
            var lines = input.Split(Environment.NewLine);
            var fileContent = new TestFileContent(lines);

            // Execute
            var isLegacy = fileContent.IsLegacyProjectStructureUsed;
            var isNew = fileContent.IsNewProjectStructureUsed;

            // Verify
            Assert.True(isLegacy);
            Assert.False(isNew);
        }

        [Fact]
        public void TestFileContent_NewProjectStructure()
        {
            // Setup
            var input =
            """
            <Project Sdk="Microsoft.NET.Sdk">
             <PropertyGroup>
              <TargetFrameworks>net461;net70</TargetFrameworks>
             </PropertyGroup>
            </Project>
            """;
            var lines = input.Split(Environment.NewLine);

            var fileContent = new TestFileContent(lines);

            // Execute
            var isLegacy = fileContent.IsLegacyProjectStructureUsed;
            var isNew = fileContent.IsNewProjectStructureUsed;

            // Verify
            Assert.True(isNew);
            Assert.False(isLegacy);
        }

        [Fact]
        public void TestFileContent_XmlSyntax()
        {
            var input =
            """
            <msb:Project xmlns:msb="http://schemas.microsoft.com/developer/msbuild/2003"
                         Sdk='Microsoft.NET.Sdk'>
              <msb:ItemGroup>
                <msb:PackageReference
                    Version='1.0' Include='My.Package' />
                <msb:PackageReference Include='Other.Package'><Version>2.0</Version></msb:PackageReference>
                <msb:PackageReference Update='Ignored.Package' />
                <msb:PackageReference Include='' />
                <msb:FrameworkReference
                    Include='Microsoft.AspNetCore.App' />
                <msb:Using Include='My.&#78;amespace' />
              </msb:ItemGroup>
              <msb:PropertyGroup>
                <msb:ImplicitUsings>
                  ENABLE
                </msb:ImplicitUsings><msb:UseWPF> true </msb:UseWPF><msb:UseWindowsForms><![CDATA[true]]></msb:UseWindowsForms>
              </msb:PropertyGroup>
            </msb:Project>
            """;
            var fileContent = new TestFileContent(new[] { input });

            Assert.Equal(2, fileContent.AllPackages.Count);
            Assert.Contains(new PackageReference("my.package", PackageReferenceSource.SdkCsProj), fileContent.AllPackages);
            Assert.Contains(new PackageReference("other.package", PackageReferenceSource.SdkCsProj), fileContent.AllPackages);
            Assert.True(fileContent.IsAspNetCoreDetected);
            Assert.True(fileContent.UseImplicitUsings);
            Assert.True(fileContent.UseWpf);
            Assert.True(fileContent.UseWindowsForms);
            Assert.Contains("My.Namespace", fileContent.CustomImplicitUsings);
        }

        [Fact]
        public void TestFileContent_CommentsAreIgnored()
        {
            var input =
            """
            <Project>
              <!--
              <Project Sdk="Microsoft.NET.Sdk.Web" />
              <PackageReference Include="Ignored.Package" />
              <FrameworkReference Include="Microsoft.AspNetCore.App" />
              <ImplicitUsings>true</ImplicitUsings>
              <UseWPF>true</UseWPF>
              <UseWindowsForms>true</UseWindowsForms>
              <Using Include="Ignored.Namespace" />
              <Import Project="Microsoft.CSharp.targets" />
              -->
            </Project>
            """;
            var fileContent = new TestFileContent(new[] { input });

            Assert.Empty(fileContent.AllPackages);
            Assert.Empty(fileContent.CustomImplicitUsings);
            Assert.False(fileContent.UseAspNetCoreDlls);
            Assert.False(fileContent.UseImplicitUsings);
            Assert.False(fileContent.UseWpf);
            Assert.False(fileContent.UseWindowsForms);
            Assert.False(fileContent.IsLegacyProjectStructureUsed);
            Assert.False(fileContent.IsNewProjectStructureUsed);
        }

        [Fact]
        public void TestFileContent_PackagesConfig()
        {
            var input =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <packages>
              <package
                  version='1.0' id='My.Package' />
              <package id="Other.Package" version="2.0" />
              <package id="" />
              <package />
              <!-- <package id="Ignored.Package" /> -->
              <PackageReference Include="Ignored.Package" />
            </packages>
            """;
            var fileContent = new TestFileContent(new[] { input }, "PACKAGES.CONFIG");

            Assert.Equal(2, fileContent.AllPackages.Count);
            Assert.Contains(new PackageReference("my.package", PackageReferenceSource.PackagesConfig), fileContent.AllPackages);
            Assert.Contains(new PackageReference("other.package", PackageReferenceSource.PackagesConfig), fileContent.AllPackages);
        }

        [Fact]
        public void TestFileContent_CaseInsensitive()
        {
            var fileContent = new TestFileContent(new[]
            {
                "<project sdk='microsoft.net.sdk.web'><packagereference include='My.Package' /><usewpf>TRUE</usewpf></project>"
            });

            Assert.True(fileContent.IsAspNetCoreDetected);
            Assert.True(fileContent.UseWpf);
            Assert.Contains(new PackageReference("my.package", PackageReferenceSource.SdkCsProj), fileContent.AllPackages);
        }

        [Fact]
        public void TestFileContent_ReadsOnceAndDisposesStream()
        {
            var stub = new UnsafeFileReaderStub(new Dictionary<string, string>
            {
                ["test.csproj"] = "<Project Sdk='Microsoft.NET.Sdk.Web' />"
            });
            var fileContent = new FileContent(new LoggerStub(), new[] { "test.csproj" }, stub);

            Assert.Equal(0, stub.OpenCount);
            Assert.True(fileContent.UseAspNetCoreDlls);
            Assert.True(fileContent.IsNewProjectStructureUsed);
            Assert.Empty(fileContent.AllPackages);
            Assert.Equal(1, stub.OpenCount);
            Assert.NotNull(stub.LastStream);
            Assert.False(stub.LastStream.CanRead);
        }

        private sealed class RecordingLogger : ILogger
        {
            public List<string> Messages { get; } = new List<string>();
            public void Log(Severity severity, string message, int? threadId = null) => Messages.Add(message);
            public void Dispose() { }
        }

        [Theory]
        [InlineData("This is not XML.")]
        [InlineData("<Project><PackageReference Include='Incomplete'")]
        [InlineData("<!DOCTYPE Project [<!ENTITY sdk 'Microsoft.NET.Sdk.Web'>]><Project Sdk='&sdk;' />")]
        public void TestFileContent_InvalidXmlDoesNotPreventReadingOtherFiles(string invalidXml)
        {
            var logger = new RecordingLogger();
            var stub = new UnsafeFileReaderStub(new Dictionary<string, string>
            {
                ["invalid.csproj"] = invalidXml,
                ["valid.csproj"] = "<Project Sdk='Microsoft.NET.Sdk.Web'><PackageReference Include='My.Package' /></Project>"
            });
            var fileContent = new FileContent(logger, new[] { "invalid.csproj", "valid.csproj" }, stub);

            Assert.True(fileContent.IsAspNetCoreDetected);
            Assert.Contains(new PackageReference("my.package", PackageReferenceSource.SdkCsProj), fileContent.AllPackages);
            Assert.Contains("Failed to read file invalid.csproj", logger.Messages);
            Assert.Equal(2, stub.OpenCount);
        }
    }
}
