using AssetImport;
using System.Text.Json;

internal static class MappingChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var model=new PmxModel { Bones=new[] {
            new PmxModel.Bone { Name="root", Parent=-1, Flags=6 },
            new PmxModel.Bone { Name="append", Parent=0, Flags=0x102, GrantParent=2, GrantWeight=.5f, Layer=1 },
            new PmxModel.Bone { Name="source", Parent=0, Flags=2 },
            new PmxModel.Bone { Name="hair", Parent=0, Flags=2 },
            new PmxModel.Bone { Name="hair_tip", Parent=3, Flags=2 }
        }, Morphs=Array.Empty<PmxModel.Morph>() };
        var targets=new[] { new PmxMorphTarget {Renderer="split",Shape="blink"},new PmxMorphTarget {Renderer="split",Shape="blink"} };
        PmxMmddResult Build(int[] roots)=>PmxMmddMapping.Build(model,new byte[]{1},"original test",targets,roots);
        var normal=Build(Array.Empty<int>());
        var json=JsonDocument.Parse(normal.MappingJson).RootElement;
        var order=json.GetProperty("bones").EnumerateArray().Select(b=>b.GetProperty("vmdName").GetString()).ToArray();
        check(Array.IndexOf(order,"source")<Array.IndexOf(order,"append"),"grant dependency before consumer despite source index order");
        check(json.GetProperty("morphs")[0].GetProperty("funcList").GetArrayLength()==1,"duplicate renderer chunks need one broadcasting FUNC");
        var variant=Build(new[]{3});
        check(variant.BoneCount==3 && variant.ModelId!=normal.ModelId,"selected physics root excludes descendants and changes mapping identity");
        var withoutMorphs=PmxMmddMapping.Build(model,new byte[]{1},"original test",Array.Empty<PmxMorphTarget>(),Array.Empty<int>());
        check(withoutMorphs.ModelId!=normal.ModelId,"different BlendShape options do not overwrite each other's map");
        void Reject(Action action,string reason) {try {action();throw new Exception("Accepted "+reason);}catch(FormatException){check(true,reason);}}
        Reject(()=>Build(new[]{0}),"all bones selected as physics");
        Reject(()=>Build(new[]{2}),"grant depends on a physics bone");
        model.Bones[2].Flags|=0x100; model.Bones[2].GrantParent=1;
        Reject(()=>Build(Array.Empty<int>()),"cyclic append dependency");
        model.Bones[2].Flags=2;model.Bones[2].Name="root";
        Reject(()=>Build(Array.Empty<int>()),"ambiguous original motion names");
        model.Bones[2].Name="source"; model.Bones[1].Flags|=0x200;
        Reject(()=>Build(Array.Empty<int>()),"unsupported position append");
    }
}
