﻿using ParserUtils;
using Sprache;

namespace KarelParser;

// TODO: pretty printing of types
public sealed record KarelType(string Identifier, KarelUserType Type, string FromProgram)
    : WithPosition, IKarelParser<KarelType>
{
    public override string ToString()
    {
        var fromPart = string.IsNullOrEmpty(FromProgram) ? "" : $" FROM {FromProgram}";
        return $"{Identifier}{fromPart} = {Type}";
    }

    private static Parser<KarelType> InternalParser()
        => from ident in KarelCommon.Identifier
           from program in KarelCommon.Keyword("FROM")
               .Then(_ => KarelCommon.Identifier).Optional()
           from sep in KarelCommon.Keyword("=")
           from userType in KarelUserType.GetParser()
           select new KarelType(ident, userType, program.GetOrElse(string.Empty));

    public static Parser<KarelType> GetParser()
        => InternalParser().WithPos();
}

public record KarelUserType : WithPosition, IKarelParser<KarelUserType>
{
    private static Parser<KarelUserType> InternalParser()
        => KarelStructure.GetParser()
            .Or(KarelDataType.GetParser());

    public static Parser<KarelUserType> GetParser()
        => InternalParser().WithPos();
}

public record KarelDataType
    : KarelUserType, IKarelParser<KarelDataType>
{
    private static Parser<KarelDataType> InternalParser()
        => KarelTypeString.GetParser()
            .Or(KarelTypeArray.GetParser())
            .Or(KarelTypePosition.GetParser())
            .Or(KarelTypePath.GetParser())
            .Or(KarelTypeName.GetParser());

    public new static Parser<KarelDataType> GetParser()
        => InternalParser().WithPos();
}

public sealed record KarelTypeName(string Identifier, int? Group)
    : KarelDataType, IKarelParser<KarelDataType>
{
    public override string ToString()
    {
        return Group.HasValue ? $"{Identifier} IN GROUP\\[{Group.Value}\\]" : Identifier;
    }

    public new static Parser<KarelDataType> GetParser()
        => from ident in KarelCommon.Identifier.Or(KarelCommon.Reserved)
           from grp in (from kw in KarelCommon.Keyword("IN")
                        from kww in KarelCommon.Keyword("GROUP")
                        from grp in Parse.Number.BetweenBrackets().Select(int.Parse)
                        select grp).Optional()
           select new KarelTypeName(ident, grp.IsDefined ? grp.Get() : null);
}

public sealed record KarelTypeString(int Size)
    : KarelDataType, IKarelParser<KarelDataType>
{
    public override string ToString()
    {
        return $"STRING\\[{Size}\\]";
    }

    public new static Parser<KarelDataType> GetParser()
        => from kw in KarelCommon.Keyword("STRING")
           from size in Parse.Number.BetweenBrackets().Select(int.Parse)
           select new KarelTypeString(size);
}

public sealed record KarelTypeArray(List<KarelValue> Size, KarelDataType Type)
    : KarelDataType, IKarelParser<KarelDataType>
{
    public override string ToString()
    {
        var sizeStr = Size.Count > 0 ? string.Join(",", Size.Select(s => s.ToString())) : "";
        return $"ARRAY\\[{sizeStr}\\] OF {Type}";
    }

    public new static Parser<KarelDataType> GetParser()
        => from kw in KarelCommon.Keyword("ARRAY")
           from size in KarelInteger.GetParser().Or(KarelVariableAccess.GetParser())
            .DelimitedBy(Parse.Char(','), 1, int.MaxValue)
            .BetweenBrackets().Optional()
           from sep in KarelCommon.Keyword("OF")
           from type in Parse.Ref(() => KarelDataType.GetParser())
           select new KarelTypeArray(size.GetOrElse([]).ToList(), (KarelDataType)type);
}

public sealed record KarelTypePosition(string PosType, int Group)
    : KarelDataType, IKarelParser<KarelDataType>
{
    public override string ToString()
    {
        return $"{PosType} IN GROUP[{Group}]";
    }

    public new static Parser<KarelDataType> GetParser()
        => from posType in KarelCommon.Identifier
           from sep in KarelCommon.Keyword("IN")
           from grp in KarelCommon.Keyword("GROUP")
           from num in Parse.Number.Select(int.Parse).BetweenBrackets()
           select new KarelTypePosition(posType, num);
}

public sealed record KarelTypePath(string Header, string nodeData)
    : KarelDataType, IKarelParser<KarelDataType>
{
    public override string ToString()
    {
        var headerPart = string.IsNullOrEmpty(Header) ? "" : $"PATHHEADER = {Header}, ";
        return $"PATH {headerPart}NODEDATA = {nodeData}";
    }

    public new static Parser<KarelDataType> GetParser()
        => from kw in KarelCommon.Keyword("PATH")
           from header in (
                   from kww in KarelCommon.Keyword("PATHHEADER")
                   from sep in KarelCommon.Keyword("=")
                   from ident in KarelCommon.Identifier
                   from sep2 in KarelCommon.Keyword(",")
                   select ident
                   ).Optional()
           from kwww in KarelCommon.Keyword("NODEDATA")
           from sep in KarelCommon.Keyword("=")
           from node in KarelCommon.Identifier
           select new KarelTypePath(header.GetOrDefault(), node);

}

public sealed record KarelStructure(List<KarelField> Fields)
    : KarelUserType, IKarelParser<KarelUserType>
{
    public override string ToString()
    {
        var fieldsStr = string.Join("\n", Fields.Select(f => f.ToString()));
        return $"STRUCTURE\n{fieldsStr}\nENDSTRUCTURE";
    }

    private static Parser<KarelStructure> InternalParser()
        => from structOpen in KarelCommon.Keyword("STRUCTURE")
           from fields in KarelField.GetParser().IgnoreComments().AtLeastOnce()
           from structClose in KarelCommon.Keyword("ENDSTRUCTURE")
           select new KarelStructure(fields.ToList());

    public new static Parser<KarelUserType> GetParser()
        => InternalParser().WithPos();
}

public record KarelField(string Identifier, KarelDataType Type)
    : WithPosition, IKarelParser<KarelField>
{
    public override string ToString()
    {
        return $"    {Identifier} : {Type}";
    }

    private static Parser<KarelField> InternalParser()
        => from ident in KarelCommon.Identifier
           from sep in KarelCommon.Keyword(":")
           from type in Parse.Ref(KarelDataType.GetParser)
           select new KarelField(ident, type);

    public static Parser<KarelField> GetParser()
        => InternalParser().WithPos();
}
