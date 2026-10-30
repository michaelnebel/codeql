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
        public TestFileContent(string content, string file = "test1.csproj") : base(new LoggerStub(),
            new[] { file },
            new UnsafeFileReaderStub(new Dictionary<string, string> { [file] = content }))
        { }
    }

    public class FileContentTests
    {
        [Fact]
        public void TestFileContent1()
        {
            var fileContent = new TestFileContent(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PackageReference Include="DotNetAnalyzers.DocumentationAnalyzers" Version="1.0.0-beta.59" PrivateAssets="all" />
              <PackageReference Version="7.0.0" Include="Microsoft.CodeAnalysis.NetAnalyzers" PrivateAssets="all" />
              <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.406"></PackageReference>
              <!-- <PackageReference Include="NUnit" Version="3.10.1" /> -->
              <FrameworkReference Include="My.Framework"/>
            </Project>
            """);

            Assert.False(fileContent.UseAspNetCoreDlls);
            Assert.Equal(3, fileContent.AllPackages.Count);
            Assert.Contains(new PackageReference("dotnetanalyzers.documentationanalyzers", PackageReferenceSource.SdkCsProj), fileContent.AllPackages);
            Assert.Contains(new PackageReference("microsoft.codeanalysis.netanalyzers", PackageReferenceSource.SdkCsProj), fileContent.AllPackages);
            Assert.Contains(new PackageReference("stylecop.analyzers", PackageReferenceSource.SdkCsProj), fileContent.AllPackages);
        }

        [Fact]
        public void TestFileContent2()
        {
            var fileContent = new TestFileContent(
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <FrameworkReference Include="My.Framework"/>
              <PackageReference Version="7.0.0" Include="Microsoft.CodeAnalysis.NetAnalyzers" PrivateAssets="all" />
              <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.406" />
            </Project>
            """);

            Assert.True(fileContent.UseAspNetCoreDlls);
            Assert.Equal(2, fileContent.AllPackages.Count);
            Assert.Contains(new PackageReference("microsoft.codeanalysis.netanalyzers", PackageReferenceSource.SdkCsProj), fileContent.AllPackages);
            Assert.Contains(new PackageReference("stylecop.analyzers", PackageReferenceSource.SdkCsProj), fileContent.AllPackages);
        }

        private static void AssertSetting(string xml, Func<FileContent, bool> setting, bool expected)
        {
            Assert.Equal(expected, setting(new TestFileContent(xml)));
        }

        [Theory]
        [InlineData("false", false)]
        [InlineData("true", true)]
        [InlineData("enable", true)]
        public void TestFileContent_ImplicitUsings(string value, bool expected)
        {
            AssertSetting($"<Project><ImplicitUsings>{value}</ImplicitUsings></Project>", fc => fc.UseImplicitUsings, expected);
        }

        [Theory]
        [InlineData("false", false)]
        [InlineData("true", true)]
        public void TestFileContent_UseWpf(string value, bool expected)
        {
            AssertSetting($"<Project><UseWPF>{value}</UseWPF></Project>", fc => fc.UseWpf, expected);
        }

        [Theory]
        [InlineData("false", false)]
        [InlineData("true", true)]
        public void TestFileContent_UseWindowsForms(string value, bool expected)
        {
            AssertSetting($"<Project><UseWindowsForms>{value}</UseWindowsForms></Project>", fc => fc.UseWindowsForms, expected);
        }

        [Fact]
        public void TestFileContent_ImplicitUsingsAdditional()
        {
            var fileContent = new TestFileContent(
            """
            <Project>
              <Using Include="Ns0.Ns1" />
              <Using Include="Ns2" />
              <Using Remove="Ns3" />
            </Project>
            """);

            Assert.Equal(2, fileContent.CustomImplicitUsings.Count);
            Assert.Contains("Ns0.Ns1", fileContent.CustomImplicitUsings);
            Assert.Contains("Ns2", fileContent.CustomImplicitUsings);
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
            var fileContent = new TestFileContent(input);

            Assert.True(fileContent.IsLegacyProjectStructureUsed);
            Assert.False(fileContent.IsNewProjectStructureUsed);
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
            var fileContent = new TestFileContent(input);

            Assert.True(fileContent.IsNewProjectStructureUsed);
            Assert.False(fileContent.IsLegacyProjectStructureUsed);
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
                </msb:ImplicitUsings><msb:UseWPF> true </msb:UseWPF>
                <msb:UseWindowsForms><![CDATA[true]]></msb:UseWindowsForms>
              </msb:PropertyGroup>
            </msb:Project>
            """;
            var fileContent = new TestFileContent(input);

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
            var fileContent = new TestFileContent(input);

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
            var fileContent = new TestFileContent(input, "PACKAGES.CONFIG");

            Assert.Equal(2, fileContent.AllPackages.Count);
            Assert.Contains(new PackageReference("my.package", PackageReferenceSource.PackagesConfig), fileContent.AllPackages);
            Assert.Contains(new PackageReference("other.package", PackageReferenceSource.PackagesConfig), fileContent.AllPackages);
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
            Assert.Contains("Failed to parse invalid.csproj as an XML file.", logger.Messages);
            Assert.Equal(2, stub.OpenCount);
        }
    }
}
