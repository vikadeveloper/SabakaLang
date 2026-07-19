using System.Text;
using SabakaLang.Compiler.AST;
using SabakaLang.Compiler.Binding;
using SabakaLang.Compiler.Lexing;
using SabakaLang.Compiler.Parsing;

namespace SabakaLang.Transpiler;

public class Transpiler
{
    private StringBuilder _sb = new();
    private int _indent;
    private SymbolTable? _symbols;

    public (string Decls, string Stmts, List<ImportStmt> importStmts) TranspileAst(ParseResult ast, Binder binder)
    {
        var bindResult = binder.Bind(ast.Statements);
        _symbols = bindResult.Symbols;

        var declsSb = new StringBuilder();
        var stmtsSb = new StringBuilder();
        
        var decls = ast.Statements.Where(IsDeclaration).ToList();
        var stmts = ast.Statements.Where(s => !IsDeclaration(s)).ToList();
        
        var imports = ast.Statements.OfType<ImportStmt>().ToList();
        
        imports.RemoveAll(s => s.Path.EndsWith(".sabaka"));
        
        stmts.RemoveAll(s => s is ImportStmt);

        var oldSb = _sb;

        _indent = 1;
        _sb = declsSb;
        foreach (var stmt in decls) TranspileStatement(stmt);

        _indent = 2;
        _sb = stmtsSb;
        foreach (var stmt in stmts) TranspileStatement(stmt);
        
        _sb = oldSb;
        return (declsSb.ToString(), stmtsSb.ToString(), imports);
    }

    public bool IsDeclaration(IStmt stmt)
    {
        return stmt is ClassDecl || stmt is FuncDecl || stmt is InterfaceDecl || stmt is StructDecl || stmt is EnumDecl;
    }

    public string Transpile(string src)
    {
        return Transpile(src, new Binder());
    }

    public string Transpile(string src, Binder binder, bool includeUsings = true)
    {
        var tokens = new Lexer(src).Tokenize();
        if (tokens.HasErrors)
        {
            // foreach (var err in tokens.Errors) Console.Error.WriteLine($"Lexer Error: {err.Message} at {err.Position}");
            return "";
        }

        var ast = new Parser(tokens).Parse();
        if (ast.HasErrors)
        {
            // foreach (var err in ast.Errors) Console.Error.WriteLine($"Parser Error: {err.Message} at {err.Position}");
            return "";
        }

        var bindResult = binder.Bind(ast.Statements);
        _symbols = bindResult.Symbols;

        _sb.Clear();
        _indent = 0;

        if (includeUsings)
        {
            _sb.AppendLine("using System;");
            _sb.AppendLine("using System.IO;");
            _sb.AppendLine("using System.Linq;");
            _sb.AppendLine("using System.Collections.Generic;");
            _sb.AppendLine("using System.Threading;");
            _sb.AppendLine();
        }

        foreach (var stmt in ast.Statements)
        {
            TranspileStatement(stmt);
        }

        return _sb.ToString();
    }

    private void TranspileStatement(IStmt stmt)
    {
        switch (stmt)
        {
            case ImportStmt import:
                TranspileImport(import);
                break;
            case VarDecl varDecl:
                WriteIndent();
                TranspileVarDecl(varDecl);
                _sb.AppendLine(";");
                break;
            case FuncDecl funcDecl:
                TranspileFuncDecl(funcDecl);
                break;
            case ClassDecl classDecl:
                TranspileClassDecl(classDecl);
                break;
            case InterfaceDecl interfaceDecl:
                TranspileInterfaceDecl(interfaceDecl);
                break;
            case StructDecl structDecl:
                TranspileStructDecl(structDecl);
                break;
            case EnumDecl enumDecl:
                TranspileEnumDecl(enumDecl);
                break;
            case IfStmt ifStmt:
                TranspileIf(ifStmt);
                break;
            case WhileStmt whileStmt:
                TranspileWhile(whileStmt);
                break;
            case ForStmt forStmt:
                TranspileFor(forStmt);
                break;
            case ForeachStmt foreachStmt:
                TranspileForeach(foreachStmt);
                break;
            case ReturnStmt returnStmt:
                WriteIndent();
                _sb.Append("return");
                if (returnStmt.Value != null)
                {
                    _sb.Append(" ");
                    TranspileExpression(returnStmt.Value);
                }
                _sb.AppendLine(";");
                break;
            case ExprStmt exprStmt:
                WriteIndent();
                TranspileExpression(exprStmt.Expr);
                _sb.AppendLine(";");
                break;
            case SwitchStmt switchStmt:
                TranspileSwitch(switchStmt);
                break;
        }
    }

    private void TranspileImport(ImportStmt import)
    {
        var path = import.Path.Trim('\"');
        if (path.EndsWith(".sabaka")) return;

        if (import.Names.Count > 0)
        {
            foreach (var name in import.Names)
            {
                _sb.Append("using ");
                if (import.Alias != null) _sb.Append($"{import.Alias} = ");
                _sb.AppendLine($"{path}.{name};");
            }
        }
        else
        {
            _sb.Append("using ");
            if (import.Alias != null) _sb.Append($"{import.Alias} = ");
            _sb.AppendLine($"{path};");
        }
    }

    private void TranspileVarDecl(VarDecl varDecl)
    {
        var kind = _symbols?.All.FirstOrDefault(s => s.Name == varDecl.Name && s.Span == varDecl.Span)?.Kind;
        var isField = kind == SymbolKind.Field;

        if (isField) _sb.Append($"{GetAccessMod(varDecl.Access)} ");
        if (varDecl.IsConst) _sb.Append("const ");
        _sb.Append($"{TranspileType(varDecl.Type)} {varDecl.Name}");
        if (varDecl.Init != null)
        {
            _sb.Append(" = ");
            TranspileExpression(varDecl.Init);
        }
    }

    private void TranspileFuncDecl(FuncDecl funcDecl)
    {
        var isTopLevel = _symbols?.All.FirstOrDefault(s => s.Name == funcDecl.Name && s.Span == funcDecl.Span)?.Kind == SymbolKind.Function;
        
        WriteIndent();
        _sb.Append($"{GetAccessMod(funcDecl.Access)} ");
        if (isTopLevel) _sb.Append("static ");
        if (funcDecl.IsOverride) _sb.Append("override ");
        _sb.Append($"{TranspileType(funcDecl.ReturnType)} {funcDecl.Name}");
        if (funcDecl.TypeParams.Count > 0)
        {
            _sb.Append("<");
            _sb.Append(string.Join(", ", funcDecl.TypeParams.Select(p => p.Name)));
            _sb.Append(">");
        }
        _sb.Append("(");
        _sb.Append(string.Join(", ", funcDecl.Params.Select(p => $"{TranspileType(p.Type)} {p.Name}")));
        _sb.AppendLine(")");
        WriteIndent();
        _sb.AppendLine("{");
        _indent++;
        foreach (var stmt in funcDecl.Body) TranspileStatement(stmt);
        _indent--;
        WriteIndent();
        _sb.AppendLine("}");
    }

    private void TranspileClassDecl(ClassDecl classDecl)
    {
        WriteIndent();
        _sb.Append($"public class {classDecl.Name}");
        if (classDecl.TypeParams.Count > 0)
        {
            _sb.Append("<");
            _sb.Append(string.Join(", ", classDecl.TypeParams.Select(p => p.Name)));
            _sb.Append(">");
        }
        var bases = new List<string>();
        if (classDecl.Base != null) bases.Add(classDecl.Base);
        bases.AddRange(classDecl.Interfaces);
        if (bases.Count > 0) _sb.Append($" : {string.Join(", ", bases)}");
        _sb.AppendLine();
        WriteIndent();
        _sb.AppendLine("{");
        _indent++;
        foreach (var field in classDecl.Fields)
        {
            WriteIndent();
            TranspileVarDecl(field);
            _sb.AppendLine(";");
        }
        foreach (var method in classDecl.Methods) TranspileFuncDecl(method);
        _indent--;
        WriteIndent();
        _sb.AppendLine("}");
    }

    private void TranspileInterfaceDecl(InterfaceDecl iface)
    {
        WriteIndent();
        _sb.Append($"public interface {iface.Name}");
        if (iface.TypeParams.Count > 0)
        {
            _sb.Append("<");
            _sb.Append(string.Join(", ", iface.TypeParams.Select(p => p.Name)));
            _sb.Append(">");
        }
        if (iface.Parents.Count > 0) _sb.Append($" : {string.Join(", ", iface.Parents)}");
        _sb.AppendLine();
        WriteIndent();
        _sb.AppendLine("{");
        _indent++;
        foreach (var method in iface.Methods)
        {
            WriteIndent();
            _sb.Append($"{TranspileType(method.ReturnType)} {method.Name}(");
            _sb.Append(string.Join(", ", method.Params.Select(p => $"{TranspileType(p.Type)} {p.Name}")));
            _sb.AppendLine(");");
        }
        _indent--;
        WriteIndent();
        _sb.AppendLine("}");
    }

    private void TranspileStructDecl(StructDecl structDecl)
    {
        WriteIndent();
        _sb.AppendLine($"public struct {structDecl.Name}");
        WriteIndent();
        _sb.AppendLine("{");
        _indent++;
        foreach (var field in structDecl.Fields)
        {
            WriteIndent();
            TranspileVarDecl(field);
            _sb.AppendLine(";");
        }
        _indent--;
        WriteIndent();
        _sb.AppendLine("}");
    }

    private void TranspileEnumDecl(EnumDecl enumDecl)
    {
        WriteIndent();
        _sb.AppendLine($"public enum {enumDecl.Name}");
        WriteIndent();
        _sb.AppendLine("{");
        _indent++;
        foreach (var member in enumDecl.Members)
        {
            WriteIndent();
            _sb.AppendLine($"{member},");
        }
        _indent--;
        WriteIndent();
        _sb.AppendLine("}");
    }

    private void TranspileIf(IfStmt ifStmt)
    {
        WriteIndent();
        _sb.Append("if (");
        TranspileExpression(ifStmt.Condition);
        _sb.AppendLine(")");
        WriteIndent();
        _sb.AppendLine("{");
        _indent++;
        foreach (var stmt in ifStmt.Then) TranspileStatement(stmt);
        _indent--;
        WriteIndent();
        _sb.AppendLine("}");
        if (ifStmt.Else != null)
        {
            WriteIndent();
            _sb.AppendLine("else");
            WriteIndent();
            _sb.AppendLine("{");
            _indent++;
            foreach (var stmt in ifStmt.Else) TranspileStatement(stmt);
            _indent--;
            WriteIndent();
            _sb.AppendLine("}");
        }
    }

    private void TranspileWhile(WhileStmt whileStmt)
    {
        WriteIndent();
        _sb.Append("while (");
        TranspileExpression(whileStmt.Condition);
        _sb.AppendLine(")");
        WriteIndent();
        _sb.AppendLine("{");
        _indent++;
        foreach (var stmt in whileStmt.Body) TranspileStatement(stmt);
        _indent--;
        WriteIndent();
        _sb.AppendLine("}");
    }

    private void TranspileFor(ForStmt forStmt)
    {
        WriteIndent();
        _sb.Append("for (");
        if (forStmt.Init != null)
        {
            if (forStmt.Init is VarDecl vd) TranspileVarDecl(vd);
            else if (forStmt.Init is ExprStmt es) TranspileExpression(es.Expr);
        }
        _sb.Append("; ");
        if (forStmt.Condition != null) TranspileExpression(forStmt.Condition);
        _sb.Append("; ");
        if (forStmt.Step != null) TranspileExpression(forStmt.Step);
        _sb.AppendLine(")");
        WriteIndent();
        _sb.AppendLine("{");
        _indent++;
        foreach (var stmt in forStmt.Body) TranspileStatement(stmt);
        _indent--;
        WriteIndent();
        _sb.AppendLine("}");
    }

    private void TranspileForeach(ForeachStmt foreachStmt)
    {
        WriteIndent();
        _sb.Append($"foreach ({TranspileType(foreachStmt.ItemType)} {foreachStmt.ItemName} in ");
        TranspileExpression(foreachStmt.Collection);
        _sb.AppendLine(")");
        WriteIndent();
        _sb.AppendLine("{");
        _indent++;
        foreach (var stmt in foreachStmt.Body) TranspileStatement(stmt);
        _indent--;
        WriteIndent();
        _sb.AppendLine("}");
    }

    private void TranspileSwitch(SwitchStmt switchStmt)
    {
        WriteIndent();
        _sb.Append("switch (");
        TranspileExpression(switchStmt.Value);
        _sb.AppendLine(")");
        WriteIndent();
        _sb.AppendLine("{");
        _indent++;
        foreach (var c in switchStmt.Cases)
        {
            WriteIndent();
            if (c.Value == null) _sb.AppendLine("default:");
            else
            {
                _sb.Append("case ");
                TranspileExpression(c.Value);
                _sb.AppendLine(":");
            }
            _indent++;
            foreach (var stmt in c.Body) TranspileStatement(stmt);
            _indent--;
        }
        _indent--;
        WriteIndent();
        _sb.AppendLine("}");
    }

    private void TranspileCall(CallExpr c)
    {
        if (c.Callee is NameExpr ne && _symbols != null)
        {
            var sym = _symbols.All.FirstOrDefault(s => s.Name == ne.Name);
            if (sym != null && sym.Kind == SymbolKind.Function)
            {
                // If it's a function from another file, it might be in a different class.
                // But for now we just try to call it.
                // If we wrapped it in a class, we'd need to know which class.
                // Let's see if we can find the file it came from.
            }

            if (sym != null && sym.Kind == SymbolKind.BuiltIn)
            {
                switch (ne.Name)
                {
                    case "print":
                        _sb.Append("Console.WriteLine");
                        break;
                    case "input":
                        _sb.Append("Console.ReadLine()");
                        return;
                    case "sleep":
                        _sb.Append("Thread.Sleep");
                        break;
                    case "readFile":
                        _sb.Append("File.ReadAllText");
                        break;
                    case "writeFile":
                        _sb.Append("File.WriteAllText");
                        break;
                    case "appendFile":
                        _sb.Append("File.AppendAllText");
                        break;
                    case "fileExists":
                        _sb.Append("File.Exists");
                        break;
                    case "deleteFile":
                        _sb.Append("File.Delete");
                        break;
                    case "readLines":
                        _sb.Append("File.ReadAllLines");
                        break;
                    case "time":
                        _sb.Append("DateTime.Now.ToString(\"HH:mm:ss\")");
                        return;
                    case "timeMs":
                        _sb.Append("Environment.TickCount");
                        return;
                    case "sin":
                        _sb.Append("Math.Sin");
                        break;
                    case "cos":
                        _sb.Append("Math.Cos");
                        break;
                    case "tan":
                        _sb.Append("Math.Tan");
                        break;
                    case "sqrt":
                        _sb.Append("Math.Sqrt");
                        break;
                    case "abs":
                        _sb.Append("Math.Abs");
                        break;
                    case "floor":
                        _sb.Append("(int)Math.Floor");
                        break;
                    case "ceil":
                        _sb.Append("(int)Math.Ceiling");
                        break;
                    case "round":
                        _sb.Append("(int)Math.Round");
                        break;
                    case "max":
                        _sb.Append("Math.Max");
                        break;
                    case "min":
                        _sb.Append("Math.Min");
                        break;
                    case "pow":
                        _sb.Append("Math.Pow");
                        break;
                    case "log":
                        _sb.Append("Math.Log");
                        break;
                    case "rand":
                        _sb.Append("new Random().Next");
                        break;
                    case "random":
                        _sb.Append("new Random().NextDouble()");
                        return;
                    case "exit":
                        _sb.Append("Environment.Exit");
                        break;
                    case "ord":
                        _sb.Append("(int)");
                        TranspileExpression(c.Args[0]);
                        _sb.Append("[0]");
                        return;
                    case "chr":
                        _sb.Append("((char)");
                        TranspileExpression(c.Args[0]);
                        _sb.Append(").ToString()");
                        return;
                }

                _sb.Append("(");
                for (int i = 0; i < c.Args.Count; i++)
                {
                    TranspileExpression(c.Args[i]);
                    if (i < c.Args.Count - 1) _sb.Append(", ");
                }
                _sb.Append(")");
                return;
            }
        }

        TranspileExpression(c.Callee);
        _sb.Append("(");
        for (int i = 0; i < c.Args.Count; i++)
        {
            TranspileExpression(c.Args[i]);
            if (i < c.Args.Count - 1) _sb.Append(", ");
        }
        _sb.Append(")");
    }

    private void TranspileExpression(IExpr expr)
    {
        switch (expr)
        {
            case IntLit i: _sb.Append(i.Value); break;
            case FloatLit f: _sb.Append(f.Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture)); break;
            case StringLit s: _sb.Append($"\"{s.Value}\""); break;
            case CharLit c: _sb.Append($"'{c.Value}'"); break;
            case BoolLit b: _sb.Append(b.Value ? "true" : "false"); break;
            case NullLit: _sb.Append("null"); break;
            case NameExpr n: _sb.Append(n.Name); break;
            case BinaryExpr b:
                _sb.Append("(");
                TranspileExpression(b.Left);
                _sb.Append($" {GetOp(b.Op)} ");
                TranspileExpression(b.Right);
                _sb.Append(")");
                break;
            case UnaryExpr u:
                _sb.Append(GetOp(u.Op));
                TranspileExpression(u.Operand);
                break;
            case CallExpr c:
                TranspileCall(c);
                break;
            case MemberExpr m:
                TranspileExpression(m.Object);
                _sb.Append($".{m.Member}");
                break;
            case IndexExpr i:
                TranspileExpression(i.Object);
                _sb.Append("[");
                TranspileExpression(i.Index);
                _sb.Append("]");
                break;
            case ArrayExpr a:
                _sb.Append("new[] { ");
                for (int i = 0; i < a.Elements.Count; i++)
                {
                    TranspileExpression(a.Elements[i]);
                    if (i < a.Elements.Count - 1) _sb.Append(", ");
                }
                _sb.Append(" }");
                break;
            case NewExpr n:
                _sb.Append($"new {n.TypeName}");
                if (n.TypeArgs.Count > 0) _sb.Append($"<{string.Join(", ", n.TypeArgs)}>");
                _sb.Append("(");
                for (int i = 0; i < n.Args.Count; i++)
                {
                    TranspileExpression(n.Args[i]);
                    if (i < n.Args.Count - 1) _sb.Append(", ");
                }
                _sb.Append(")");
                break;
            case SuperExpr: _sb.Append("base"); break;
            case AssignExpr a:
                _sb.Append("(");
                TranspileExpression(a.Target);
                _sb.Append(" = ");
                TranspileExpression(a.Value);
                _sb.Append(")");
                break;
            case TernaryExpr t:
                _sb.Append("(");
                TranspileExpression(t.Condition);
                _sb.Append(" ? ");
                TranspileExpression(t.Then);
                _sb.Append(" : ");
                TranspileExpression(t.Else);
                _sb.Append(")");
                break;
            case CoalesceExpr c:
                TranspileExpression(c.Left);
                _sb.Append(" ?? ");
                TranspileExpression(c.Right);
                break;
            case IsExpr ie:
                TranspileExpression(ie.Left);
                _sb.Append($" is {TranspileType(ie.Right)}");
                break;
            case InterpolatedStringExpr ise:
                _sb.Append("$\"");
                foreach (var part in ise.Parts)
                {
                    if (part is StringLit sl) _sb.Append(sl.Value);
                    else
                    {
                        _sb.Append("{");
                        TranspileExpression(part);
                        _sb.Append("}");
                    }
                }
                _sb.Append("\"");
                break;
        }
    }

    private string TranspileType(TypeRef type)
    {
        var name = type.Name switch
        {
            "int" => "int",
            "float" => "double",
            "string" => "string",
            "bool" => "bool",
            "void" => "void",
            _ => type.Name
        };
        if (type.TypeArgs.Count > 0) name += $"<{string.Join(", ", type.TypeArgs)}>";
        if (type.IsArray) name += "[]";
        if (type.IsNullable) name += "?";
        return name;
    }

    private string GetAccessMod(AccessMod acc) => acc.ToString().ToLower();

    private string GetOp(TokenType op) => op switch
    {
        TokenType.Plus => "+",
        TokenType.Minus => "-",
        TokenType.Star => "*",
        TokenType.Slash => "/",
        TokenType.Percent => "%",
        TokenType.EqualEqual => "==",
        TokenType.NotEqual => "!=",
        TokenType.Less => "<",
        TokenType.LessEqual => "<=",
        TokenType.Greater => ">",
        TokenType.GreaterEqual => ">=",
        TokenType.AndAnd => "&&",
        TokenType.OrOr => "||",
        TokenType.Bang => "!",
        _ => op.ToString()
    };

    private void WriteIndent() => _sb.Append(new string(' ', _indent * 4));
}