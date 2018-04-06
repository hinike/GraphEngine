namespace GraphEngine.Jit.TypeSystem

open System

type TypeCode = 
    NULL
    | U8      | U16     | U32    | U64
    | I8      | I16     | I32    | I64
    | F32     | F64
    | BOOL 
    | CHAR    | STRING | U8STRING
    | LIST
    | STRUCT  | CELL

type AttributeDescriptor = {
    Name  : string
    Value : string
}

type MemberDescriptor = {
    Name                  : string
    Type                  : TypeDescriptor 
    Optional              : bool 
}

and TypeDescriptor = {
    TypeName              :  string 
    QualifiedName         :  string
    ElementType           :  seq<TypeDescriptor>          // non-empty for container types
    Members               :  seq<MemberDescriptor>        // non-empty for structs
    TSLAttributes         :  seq<AttributeDescriptor>     // non-empty for cell/field with attributes
    TypeCode              :  TypeCode 
}

module Builder = 
    open System.Collections.Generic
    open System.Linq
    open Trinity.Storage
    open System
    open System.Reflection

    let IsStruct (T: Type) = T.IsAnsiClass && not T.IsClass

    let (|PrimitiveType|ListType|StructType|) (T: Type) = 
        match T with
        | x when x = typeof<Unit>    -> PrimitiveType NULL
        | x when x = typeof<byte>    -> PrimitiveType U8
        | x when x = typeof<uint16>  -> PrimitiveType U16
        | x when x = typeof<uint32>  -> PrimitiveType U32
        | x when x = typeof<uint64>  -> PrimitiveType U64
        | x when x = typeof<sbyte>   -> PrimitiveType I8
        | x when x = typeof<int16>   -> PrimitiveType I16
        | x when x = typeof<int32>   -> PrimitiveType I32
        | x when x = typeof<int64>   -> PrimitiveType I64
        | x when x = typeof<float32> -> PrimitiveType F32
        | x when x = typeof<double>  -> PrimitiveType F64
        | x when x = typeof<bool>    -> PrimitiveType BOOL
        | x when x = typeof<char>    -> PrimitiveType CHAR
        | x when x = typeof<string>  -> PrimitiveType STRING
        | x when x.IsConstructedGenericType && 
                 x.GetGenericTypeDefinition() = 
                 typedefof<System.Collections.Generic.List<_>> 
                                    -> ListType   (x.GetGenericArguments() |> Array.head)
        | x when x |> IsStruct      -> StructType (x.GetFields(BindingFlags.Public ||| BindingFlags.Instance))
        | _                         -> failwith "Unexpected type"

    let rec MakeFromType(T: Type) = 
        match T with
        | PrimitiveType tcode -> { TypeDescriptor.TypeName = T.Name
                                   QualifiedName = T.AssemblyQualifiedName
                                   TSLAttributes = Seq.empty
                                   ElementType = Seq.empty
                                   Members = Seq.empty
                                   TypeCode = tcode }
        | ListType elem ->       { TypeDescriptor.TypeName = T.Name
                                   QualifiedName = T.AssemblyQualifiedName
                                   TSLAttributes = Seq.empty
                                   ElementType = seq [ elem |> MakeFromType ]
                                   Members = Seq.empty
                                   TypeCode = TypeCode.LIST }
        | StructType members ->  { TypeDescriptor.TypeName = T.Name
                                   QualifiedName = T.AssemblyQualifiedName
                                   TSLAttributes = Seq.empty
                                   ElementType = Seq.empty
                                   Members = members |> Seq.map (fun m ->
                                                                 { MemberDescriptor.Name = m.Name
                                                                   Type = MakeFromType(m.FieldType)
                                                                   Optional = false }) // XXX
                                   TypeCode = TypeCode.STRUCT }


    let MakeAttr (attr: KeyValuePair<string, string>) = 
        { AttributeDescriptor.Name = attr.Key 
          Value=attr.Value }
    let MakeMember (fieldDesc: IFieldDescriptor) = 
        { MemberDescriptor.Name = fieldDesc.Name
          Type = MakeFromType(fieldDesc.Type)
          Optional = fieldDesc.Optional }
    let Make (cellDesc: ICellDescriptor) = 
        //cellDesc.Type |> MakeFromType with x.
        let members = Seq.map MakeMember <| cellDesc.GetFieldDescriptors()
        { TypeDescriptor.TypeName = cellDesc.TypeName
          QualifiedName = cellDesc.Type.AssemblyQualifiedName
          ElementType = Seq.empty
          Members = members
          TSLAttributes = cellDesc.Attributes.Select(MakeAttr)
          TypeCode = TypeCode.CELL }