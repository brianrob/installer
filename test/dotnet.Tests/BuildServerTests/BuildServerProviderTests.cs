// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.BuildServer;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.DependencyModel.Tests;
using Moq;
using Xunit;
using LocalizableStrings = Microsoft.DotNet.BuildServer.LocalizableStrings;

namespace Microsoft.DotNet.Tests.BuildServerTests
{
    public class BuildServerProviderTests
    {
        [Fact]
        public void GivenMSBuildFlagItYieldsMSBuild()
        {
            var provider = new BuildServerProvider(
                new FileSystemMockBuilder().Build(),
                CreateEnvironmentProviderMock().Object);

            provider
                .EnumerateBuildServers(ServerEnumerationFlags.MSBuild)
                .Select(s => s.Name)
                .Should()
                .Equal(LocalizableStrings.MSBuildServer);
        }

        [Fact]
        public void GivenVBCSCompilerFlagItYieldsVBCSCompiler()
        {
            var provider = new BuildServerProvider(
                new FileSystemMockBuilder().Build(),
                CreateEnvironmentProviderMock().Object);

            provider
                .EnumerateBuildServers(ServerEnumerationFlags.VBCSCompiler)
                .Select(s => s.Name)
                .Should()
                .Equal(LocalizableStrings.VBCSCompilerServer);
        }

        [Fact]
        public void GivenRazorFlagAndNoPidDirectoryTheEnumerationIsEmpty()
        {
            var provider = new BuildServerProvider(
                new FileSystemMockBuilder().Build(),
                CreateEnvironmentProviderMock().Object);

            provider
                .EnumerateBuildServers(ServerEnumerationFlags.Razor)
                .Should()
                .BeEmpty();
        }

        [Fact]
        public void GivenNoEnvironmentVariableItUsesTheDefaultPidDirectory()
        {
            var provider = new BuildServerProvider(
                new FileSystemMockBuilder().Build(),
                CreateEnvironmentProviderMock().Object);

            provider
                .GetPidFileDirectory()
                .Value
                .Should()
                .Be(Path.Combine(
                    CliFolderPathCalculator.DotnetUserProfileFolderPath,
                    "pids",
                    "build"));
        }

        [Fact]
        public void GivenEnvironmentVariableItUsesItForThePidDirectory()
        {
            const string PidDirectory = "path/to/some/directory";

            var provider = new BuildServerProvider(
                new FileSystemMockBuilder().Build(),
                CreateEnvironmentProviderMock(PidDirectory).Object);

            provider
                .GetPidFileDirectory()
                .Value
                .Should()
                .Be(PidDirectory);
        }

        [Fact]
        public void GivenARazorPidFileItReturnsARazorBuildServer()
        {
            const int ProcessId = 1234;
            const string ServerPath = "/path/to/rzc.dll";
            const string PipeName = "some-pipe-name";

            string pidDirectory = Path.GetFullPath("var/pids/build");
            string pidFilePath = Path.Combine(pidDirectory, $"{RazorPidFile.FilePrefix}{ProcessId}");

            var fileSystemMock = new FileSystemMockBuilder()
                .AddFile(
                    pidFilePath,
                    $"{ProcessId}{Environment.NewLine}{RazorPidFile.RazorServerType}{Environment.NewLine}{ServerPath}{Environment.NewLine}{PipeName}")
                .AddFile(
                    Path.Combine(pidDirectory, $"{RazorPidFile.FilePrefix}not-a-pid-file"),
                    "not-a-pid-file")
                .Build();

            var provider = new BuildServerProvider(
                fileSystemMock,
                CreateEnvironmentProviderMock(pidDirectory).Object);

            var servers = provider.EnumerateBuildServers(ServerEnumerationFlags.Razor).ToArray();
            servers.Length.Should().Be(1);

            var razorServer = servers.First() as RazorServer;
            razorServer.Should().NotBeNull();
            razorServer.ProcessId.Should().Be(ProcessId);
            razorServer.Name.Should().Be(LocalizableStrings.RazorServer);
            razorServer.PidFile.Should().NotBeNull();
            razorServer.PidFile.Path.Value.Should().Be(pidFilePath);
            razorServer.PidFile.ProcessId.Should().Be(ProcessId);
            razorServer.PidFile.ServerPath.Value.Should().Be(ServerPath);
            razorServer.PidFile.PipeName.Should().Be(PipeName);
        }

        private Mock<IEnvironmentProvider> CreateEnvironmentProviderMock(string value = null)
        {
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("DOTNET_BUILD_PIDFILE_DIRECTORY"))
                .Returns(value);

            return provider;
        }
    }
}
