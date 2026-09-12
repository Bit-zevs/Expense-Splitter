using ExpenseSplitter.Application.Access;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Access;

public sealed class JoinCodeTests
{
    [Fact]
    public void GeneratedCodesExcludeAmbiguousCharacters()
    {
        var codes = Enumerable.Range(0, 100).Select(_ => JoinCode.Generate()).ToArray();

        Assert.All(codes, code =>
        {
            Assert.Equal(12, code.Length);
            Assert.DoesNotContain(code, character => "01ILOU".Contains(character));
            Assert.Equal(32, JoinCode.Hash(code).Length);
        });
    }
}
