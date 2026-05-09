using Xunit;

namespace SabakaLang.Runtime.UnitTests;

public class MathTests : Utilities
{
    [Theory]
    [InlineData("sin(0.0)", "0")]
    [InlineData("cos(0.0)", "1")]
    [InlineData("tan(0.0)", "0")]
    [InlineData("sqrt(16.0)", "4")]
    [InlineData("abs(-5)", "5")]
    [InlineData("abs(-5.5)", "5.5")]
    [InlineData("floor(5.9)", "5")]
    [InlineData("ceil(5.1)", "6")]
    [InlineData("round(5.5)", "6")]
    [InlineData("max(10, 20)", "20")]
    [InlineData("min(10, 20)", "10")]
    [InlineData("pow(2, 3)", "8")]
    [InlineData("log(100, 10)", "2")]
    public void MathOp_IsCorrect(string expr, string expected)
    {
        var source = $"print({expr});";
        Assert.Equal(expected, Output(source));
    }

    [Fact]
    public void Rand_IsWithinRange()
    {
        var source = @"
            var r = rand(1, 10);
            if (r >= 1 && r < 10) {
                print(""ok"");
            } else {
                print(r);
            }
        ";
        Assert.Equal("ok", Output(source));
    }
}
