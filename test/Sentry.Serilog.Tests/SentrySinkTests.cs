using System;
using System.Linq;
using NSubstitute;
using Sentry.Infrastructure;
using Sentry.Protocol;
using Sentry.Reflection;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Sentry.Serilog.Tests
{
    public class SentrySinkTests
    {
        private class Fixture
        {
            public SentrySerilogOptions Options { get; set; } = new SentrySerilogOptions();
            public IHub Hub { get; set; } = Substitute.For<IHub>();
            public Func<IHub> HubAccessor { get; set; }
            public IDisposable SdkDisposeHandle { get; set; } = Substitute.For<IDisposable>();
            public ISystemClock Clock { get; set; } = Substitute.For<ISystemClock>();
            public Scope Scope { get; } = new Scope(new SentryOptions());

            public Fixture()
            {
                Hub.IsEnabled.Returns(true);
                HubAccessor = () => Hub;
                Hub.ConfigureScope(Arg.Invoke(Scope));
            }

            public SentrySink GetSut()
                => new SentrySink(
                    Options,
                    HubAccessor,
                    SdkDisposeHandle,
                    Clock);
        }

        private readonly Fixture _fixture = new Fixture();

        [Fact]
        public void Emit_WithException_CreatesEventWithException()
        {
            var sut = _fixture.GetSut();

            var expected = new Exception("expected");

            var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, expected, MessageTemplate.Empty,
                Enumerable.Empty<LogEventProperty>());

            sut.Emit(evt);

            _fixture.Hub.Received(1)
                .CaptureEvent(Arg.Is<SentryEvent>(e => e.Exception == expected));
        }


        [Fact]
        public void Emit_WithException_BreadcrumbFromException()
        {
            var expectedException = new Exception("expected message");
            const BreadcrumbLevel expectedLevel = BreadcrumbLevel.Critical;

            var sut = _fixture.GetSut();

            var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Fatal, expectedException, MessageTemplate.Empty,
                Enumerable.Empty<LogEventProperty>());

            sut.Emit(evt);

            var b = _fixture.Scope.Breadcrumbs.First();
            Assert.Equal(b.Message, expectedException.Message);
            Assert.Equal(b.Timestamp, _fixture.Clock.GetUtcNow());
            Assert.Null(b.Category);
            Assert.Equal(b.Level, expectedLevel);
            Assert.Null(b.Type);
            Assert.Null(b.Data);
        }

        [Fact]
        public void Emit_SerilogSdk_Name()
        {
            var sut = _fixture.GetSut();

            var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null, MessageTemplate.Empty,
                Enumerable.Empty<LogEventProperty>());

            sut.Emit(evt);

            var expected = typeof(SentrySink).Assembly.GetNameAndVersion();
            _fixture.Hub.Received(1)
                .CaptureEvent(Arg.Is<SentryEvent>(e => e.Sdk.Name == Constants.SdkName
                                                       && e.Sdk.Version == expected.Version));
        }

        [Fact]
        public void Emit_SerilogSdk_Packages()
        {
            var sut = _fixture.GetSut();

            var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null, MessageTemplate.Empty,
                Enumerable.Empty<LogEventProperty>());

            SentryEvent actual = null;
            _fixture.Hub.When(h => h.CaptureEvent(Arg.Any<SentryEvent>()))
                .Do(c => actual = c.Arg<SentryEvent>());

            sut.Emit(evt);

            var expected = typeof(SentrySink).Assembly.GetNameAndVersion();

            Assert.NotNull(actual);
            var package = Assert.Single(actual.Sdk.Packages);
            Assert.Equal("nuget:" + expected.Name, package.Name);
            Assert.Equal(expected.Version, package.Version);
        }

        [Fact]
        public void Emit_LoggerLevel_Set()
        {
            const SentryLevel expectedLevel = SentryLevel.Error;

            var sut = _fixture.GetSut();

            var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null, MessageTemplate.Empty,
                Enumerable.Empty<LogEventProperty>());

            sut.Emit(evt);

            _fixture.Hub.Received(1)
                .CaptureEvent(Arg.Is<SentryEvent>(e => e.Level == expectedLevel));
        }

        [Fact]
        public void Emit_RenderedMessage_Set()
        {
            const string expected = "message";

            var sut = _fixture.GetSut();

            var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null,
                new MessageTemplateParser().Parse(expected), Enumerable.Empty<LogEventProperty>());

            sut.Emit(evt);

            _fixture.Hub.Received(1)
                .CaptureEvent(Arg.Is<SentryEvent>(e => e.LogEntry.Formatted == expected));
        }

        [Fact]
        public void Emit_HubAccessorReturnsNull_DoesNotThrow()
        {
            _fixture.HubAccessor = () => null;
            var sut = _fixture.GetSut();

            var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null, MessageTemplate.Empty,
                Enumerable.Empty<LogEventProperty>());
            sut.Emit(evt);
        }

        [Fact]
        public void Emit_DisabledHub_CaptureNotCalled()
        {
            _fixture.Hub.IsEnabled.Returns(false);
            var sut = _fixture.GetSut();

            var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null, MessageTemplate.Empty,
                Enumerable.Empty<LogEventProperty>());
            sut.Emit(evt);

            _fixture.Hub.DidNotReceive().CaptureEvent(Arg.Any<SentryEvent>());
        }

        [Fact]
        public void Emit_EnabledHub_CaptureCalled()
        {
            _fixture.Hub.IsEnabled.Returns(true);
            var sut = _fixture.GetSut();

            var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null, MessageTemplate.Empty,
                Enumerable.Empty<LogEventProperty>());
            sut.Emit(evt);

            _fixture.Hub.Received(1).CaptureEvent(Arg.Any<SentryEvent>());
        }

        [Fact]
        public void Emit_NullLogEvent_CaptureNotCalled()
        {
            var sut = _fixture.GetSut();

            sut.Emit(null);

            _fixture.Hub.DidNotReceive().CaptureEvent(Arg.Any<SentryEvent>());
        }

        [Fact]
        public void Emit_Properties_AsExtra()
        {
            const string expectedIp = "127.0.0.1";

            var sut = _fixture.GetSut();

            var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null, MessageTemplate.Empty,
                new[] { new LogEventProperty("IPAddress", new ScalarValue(expectedIp)) });

            sut.Emit(evt);

            _fixture.Hub.Received(1)
                .CaptureEvent(Arg.Is<SentryEvent>(e => e.Extra["IPAddress"].ToString() == expectedIp));
        }

        [Fact]
        public void Close_DisposesSdk()
        {
            var sut = _fixture.GetSut();

            var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null, MessageTemplate.Empty,
                Enumerable.Empty<LogEventProperty>());
            sut.Emit(evt);

            _fixture.SdkDisposeHandle.DidNotReceive().Dispose();

            sut.Dispose();

            _fixture.SdkDisposeHandle.Received(1).Dispose();
        }

        [Fact]
        public void Close_NoDisposeHandleProvided_DoesNotThrow()
        {
            _fixture.SdkDisposeHandle = null;
            var sut = _fixture.GetSut();
            sut.Dispose();
        }

        [Fact]
        public void Emit_WithFormat_EventCaptured()
        {
            const string expectedMessage = "Test {structured} log";
            const int param = 10;

            var sut = _fixture.GetSut();

            var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null,
                new MessageTemplateParser().Parse(expectedMessage),
                new[] { new LogEventProperty("structured", new ScalarValue(param)) });

            sut.Emit(evt);

            _fixture.Hub.Received(1).CaptureEvent(Arg.Is<SentryEvent>(p =>
                p.LogEntry.Formatted == $"Test {param} log"
                && p.LogEntry.Message == expectedMessage));
        }
    }
}
