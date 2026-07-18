namespace SabakaLang.Transpiler.UnitTests;

public class TranspilerTests
{
    [Fact]
    public async Task Transpile_SimpleVarDecl_ReturnsCorrectCSharp()
    {
        var transpiler = new Transpiler();
        var code = "int x = 5;";
        var result = transpiler.Transpile(code);
        Assert.Contains("int x = 5;", result);
        Assert.DoesNotContain("public int x = 5;", result);
    }

    [Fact]
    public async Task Transpile_FuncDecl_ReturnsCorrectCSharp()
    {
        var transpiler = new Transpiler();
        var code = "void foo(int a) { return a + 1; }";
        var result = transpiler.Transpile(code);
        Assert.Contains("public void foo(int a)", result);
        Assert.Contains("return (a + 1);", result);
    }

    [Fact]
    public async Task Transpile_ClassDecl_ReturnsCorrectCSharp()
    {
        var transpiler = new Transpiler();
        var code = "class Point { int x; int y; void move(int dx, int dy) { x = x + dx; y = y + dy; } }";
        var result = transpiler.Transpile(code);
        Assert.Contains("public class Point", result);
        Assert.Contains("public int x;", result);
        Assert.Contains("public void move(int dx, int dy)", result);
    }

    [Fact]
    public async Task Transpile_Import_ReturnsUsings()
    {
        var transpiler = new Transpiler();
        var code = "import \"System\"; import \"System.Collections.Generic\" from List;";
        var result = transpiler.Transpile(code);
        Assert.Contains("using System;", result);
        Assert.Contains("using System.Collections.Generic.List;", result);
    }

    [Fact]
    public async Task Transpile_IfElse_ReturnsCorrectCSharp()
    {
        var transpiler = new Transpiler();
        var code = "if (x > 0) { return 1; } else { return 0; }";
        var result = transpiler.Transpile(code);
        Assert.Contains("if ((x > 0))", result);
        Assert.Contains("else", result);
    }

    [Fact]
    public async Task Transpile_ForLoop_ReturnsCorrectCSharp()
    {
        var transpiler = new Transpiler();
        var code = "for (int i = 0; i < 10; i = i + 1) { print(i); }";
        var result = transpiler.Transpile(code);
        Assert.Contains("for (int i = 0; (i < 10); (i = (i + 1)))", result);
    }

    [Fact]
    public async Task Transpile_InterpolatedString_ReturnsCorrectCSharp()
    {
        var transpiler = new Transpiler();
        var code = "string s = $\"hello {name}!\";";
        var result = transpiler.Transpile(code);
        Assert.Contains("$\"hello {name}!\"", result);
    }
    [Fact]
    public void Transpile_BuiltIns()
    {
        var transpiler = new Transpiler();
        var src = @"
print(""hello"");
int x = floor(3.14);
float s = sin(1.0);
";
        var result = transpiler.Transpile(src);
        Assert.Contains("Console.WriteLine(\"hello\");", result);
        Assert.Contains("int x = (int)Math.Floor(3.14);", result);
        Assert.Contains("Math.Sin(1)", result);
    }
}
