﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using Should;
using Xunit;

namespace ScriptCs.Tests
{
    public class FileProcessorTests
    {
        public class ProcessFileMethod
        {
            private List<string> _file1 = new List<string>
                {
                    @"#load ""script2.csx""",
                    @"#load ""script4.csx"";",
                    "using System;",
                    @"Console.WriteLine(""Hello Script 1"");",
                    @"Console.WriteLine(""Loading Script 2"");",
                    @"Console.WriteLine(""Loaded Script 2"");",
                    @"Console.WriteLine(""Loading Script 4"");",
                    @"Console.WriteLine(""Loaded Script 4"");",
                    @"Console.WriteLine(""Goodbye Script 1"");"
                };

            private List<string> _file2 = new List<string>
                {
                    "using System;",
                    @"Console.WriteLine(""Hello Script 2"");",
                    @"Console.WriteLine(""Loading Script 3"");",
                    @"#load ""script3.csx""",
                    @"Console.WriteLine(""Loaded Script 3"");",
                    @"Console.WriteLine(""Goodbye Script 2"");"
                };

            private readonly List<string> _file3 = new List<string>
                {
                    "using System;",
                    "using System.Collections.Generic;",
                    @"Console.WriteLine(""Hello Script 3"");",
                    @"Console.WriteLine(""Goodbye Script 3"");"
                };

            private readonly List<string> _file4 = new List<string>
                {
                    "using System;",
                    "using System.Core;",
                    @"Console.WriteLine(""Hello Script 4"");",
                    @"Console.WriteLine(""Goodbye Script 4"");"
                };

            private readonly Mock<IFileSystem> _fileSystem;

            public ProcessFileMethod()
            {
                _fileSystem = new Mock<IFileSystem>();
                _fileSystem.SetupGet(x => x.NewLine).Returns(Environment.NewLine);
                _fileSystem.Setup(x => x.ReadFileLines(It.Is<string>(f => f == "\\script1.csx")))
                           .Returns(_file1.ToArray());
                _fileSystem.Setup(x => x.ReadFileLines(It.Is<string>(f => f == "\\script2.csx")))
                           .Returns(_file2.ToArray());
                _fileSystem.Setup(x => x.ReadFileLines(It.Is<string>(f => f == "\\script3.csx")))
                           .Returns(_file3.ToArray());
                _fileSystem.Setup(x => x.ReadFileLines(It.Is<string>(f => f == "\\script4.csx")))
                           .Returns(_file4.ToArray());
            }

            [Fact]
            public void MultipleUsingStatementsShouldProduceDistinctOutput()
            {
                var processor = new FilePreProcessor(_fileSystem.Object);
                var output = processor.ProcessFile("\\script1.csx");

                var splitOutput = output.Split(new[] {Environment.NewLine}, StringSplitOptions.None);

                _fileSystem.Verify(x => x.ReadFileLines(It.Is<string>(i => i.StartsWith("\\script"))), Times.Exactly(3));
                Assert.Equal(2, splitOutput.Count(x => x.TrimStart(' ').StartsWith("using ")));
            }

            [Fact]
            public void UsingStateMentsShoulAllBeAtTheTop()
            {
                var processor = new FilePreProcessor(_fileSystem.Object);
                var output = processor.ProcessFile("\\script1.csx");

                var splitOutput = output.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
                var lastUsing = splitOutput.ToList().FindLastIndex(x => x.TrimStart(' ').StartsWith("using "));
                var firsNotUsing = splitOutput.ToList().FindIndex(x => !x.TrimStart(' ').StartsWith("using "));

                Assert.True(lastUsing < firsNotUsing);
            }

            [Fact]
            public void ShouldNotLoadInlineLoads()
            {
                var processor = new FilePreProcessor(_fileSystem.Object);
                var output = processor.ProcessFile("\\script1.csx");

                _fileSystem.Verify(x => x.ReadFileLines(It.Is<string>(i => i == "\\script1.csx")), Times.Once());
                _fileSystem.Verify(x => x.ReadFileLines(It.Is<string>(i => i == "\\script2.csx")), Times.Once());
                _fileSystem.Verify(x => x.ReadFileLines(It.Is<string>(i => i == "\\script3.csx")), Times.Never());
                _fileSystem.Verify(x => x.ReadFileLines(It.Is<string>(i => i == "\\script4.csx")), Times.Once());
            }

            [Fact]
            public void ShouldNotLoadSameFileTwice()
            {
                var file = new List<string>
                    {
                        @"#load ""script4.csx""",
                        "using System;",
                        @"Console.WriteLine(""Hello Script 2"");",
                    };

                var fs = new Mock<IFileSystem>();
                fs.Setup(i => i.NewLine).Returns(Environment.NewLine);
                fs.Setup(x => x.ReadFileLines(It.Is<string>(f => f == "\\script2.csx")))
                  .Returns(file.ToArray());
                fs.Setup(x => x.ReadFileLines(It.Is<string>(f => f == "\\script4.csx")))
                  .Returns(_file4.ToArray());

                var processor = new FilePreProcessor(_fileSystem.Object);
                var output = processor.ProcessFile("\\script1.csx");

                _fileSystem.Verify(x => x.ReadFileLines(It.Is<string>(i => i == "\\script1.csx")), Times.Once());
                _fileSystem.Verify(x => x.ReadFileLines(It.Is<string>(i => i == "\\script2.csx")), Times.Once());
                _fileSystem.Verify(x => x.ReadFileLines(It.Is<string>(i => i == "\\script3.csx")), Times.Never());
                _fileSystem.Verify(x => x.ReadFileLines(It.Is<string>(i => i == "\\script4.csx")), Times.Once());
            }

            [Fact]
            public void LoadBeforeUsingShouldBeAllowed()
            {
                var file = new List<string>
                    {
                        @"#load ""script4.csx""",
                        "",
                        "using System;",
                        @"Console.WriteLine(""abc"");"
                    };

                _fileSystem.Setup(x => x.ReadFileLines(It.Is<string>(f => f == "\\file.csx"))).Returns(file.ToArray());

                var processor = new FilePreProcessor(_fileSystem.Object);
                var output = processor.ProcessFile("\\file.csx");

                var splitOutput = output.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
                var lastUsing = splitOutput.ToList().FindLastIndex(x => x.TrimStart(' ').StartsWith("using "));
                var firsNotUsing = splitOutput.ToList().FindIndex(x => !x.TrimStart(' ').StartsWith("using "));

                splitOutput.Count(x => x.TrimStart(' ').StartsWith("using ")).ShouldEqual(2);
                Assert.True(lastUsing < firsNotUsing);
            }

            [Fact]
            public void ShouldNotBeAllowedToLoadAfterUsing()
            {
                var file = new List<string>
                    {
                        "using System;",
                        @"Console.WriteLine(""abc"");",
                        @"#load ""script4.csx"""
                    };
                _fileSystem.Setup(x => x.ReadFileLines(It.Is<string>(f => f == "\\file.csx"))).Returns(file.ToArray());

                var processor = new FilePreProcessor(_fileSystem.Object);
                var output = processor.ProcessFile("\\file.csx");

                var splitOutput = output.Split(new[] {Environment.NewLine}, StringSplitOptions.None);

                Assert.Equal(1, splitOutput.Count(x => x.TrimStart(' ').StartsWith("using ")));
                Assert.Equal(3, splitOutput.Length);
                _fileSystem.Verify(x => x.ReadFileLines(It.Is<string>(i => i == "\\script3.csx")), Times.Never());
            }

            [Fact]
            public void UsingInCodeDoesNotCountAsUsingImport()
            {
                var file = new List<string>
                    {
                        @"#load ""script4.csx""",
                        "",
                        "using System;",
                        "using System.IO;",
                        "Console.WriteLine();",
                        @"using (var stream = new MemoryStream) {",
                        @"//do stuff",
                        @"}"
                    };
                _fileSystem.Setup(x => x.ReadFileLines(It.Is<string>(f => f == "\\file.csx"))).Returns(file.ToArray());

                var processor = new FilePreProcessor(_fileSystem.Object);
                var output = processor.ProcessFile("\\file.csx");

                var splitOutput = output.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
                var firstNonImportUsing =
                    splitOutput.ToList().FindIndex(x => x.TrimStart(' ').StartsWith("using ") && !x.Contains(";"));
                var firstCodeLine = splitOutput.ToList().FindIndex(x => x.Contains("Console"));

                Assert.True(firstNonImportUsing > firstCodeLine);
            }

            [Fact]
            public void ShouldHaveReferencesOnTop()
            {
                var file1 = new List<string>
                    {
                        @"#r ""My.dll""",
                        @"#load ""script2.csx""",
                        "using System;",
                        @"Console.WriteLine(""Hi!"");"
                    };

                var fs = new Mock<IFileSystem>();
                fs.Setup(i => i.NewLine).Returns(Environment.NewLine);
                fs.Setup(x => x.ReadFileLines(It.Is<string>(f => f == "\\script1.csx"))).Returns(file1.ToArray());
                fs.Setup(x => x.ReadFileLines(It.Is<string>(f => f == "\\script2.csx"))).Returns(_file2.ToArray());

                var processor = new FilePreProcessor(fs.Object);
                var output = processor.ProcessFile("\\script1.csx");
                var splitOutput = output.Split(new[] {Environment.NewLine}, StringSplitOptions.None).ToList();

                var lastR = splitOutput.FindLastIndex(line => line.StartsWith("#r "));
                var firstNotR = splitOutput.FindIndex(line => !line.StartsWith("#r "));

                lastR.ShouldNotEqual(-1);
                Assert.True(lastR < firstNotR);
            }

            [Fact]
            public void ShouldHaveReferencesFromAllFiles()
            {
                var file1 = new List<string>
                    {
                        @"#r ""My.dll""",
                        @"#load ""scriptX.csx""",
                        "using System;",
                        @"Console.WriteLine(""Hi!"");"
                    };

                var file2 = new List<string>
                    {
                        @"#r ""My2.dll""",
                        "using System;",
                        @"Console.WriteLine(""Hi!"");"
                    };

                _fileSystem.Setup(x => x.ReadFileLines(It.Is<string>(f => f == "\\script1.csx")))
                           .Returns(file1.ToArray());
                _fileSystem.Setup(x => x.ReadFileLines(It.Is<string>(f => f == "\\scriptX.csx")))
                           .Returns(file2.ToArray());

                var processor = new FilePreProcessor(_fileSystem.Object);
                var output = processor.ProcessFile("\\script1.csx");

                var splitOutput = output.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
                splitOutput.Count(line => line.StartsWith("#r ")).ShouldEqual(2);
            }
        }
    }
}