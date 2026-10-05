using System;
using System.Collections.Generic;
using Assimp;
#if KK
using Vec = Assimp.Vector3D;
using Mat = Assimp.Matrix4x4;
using Colour = Assimp.Color4D;
#else
using Vec = System.Numerics.Vector3;
using Mat = System.Numerics.Matrix4x4;
using Colour = System.Numerics.Vector4;
#endif

namespace AssetImport
{
    internal static class PmxSceneBuilder
    {
        // PMX/MMD units -> KK MMDD units. Rotate 180 degrees around Y once here;
        // FinishLoadProcess must not apply the generic import's extra rotation.
        internal const float UnitScale = .0832f;
        internal static Vec Convert(PmxModel.V3 v, bool scale)
        { float s=scale ? UnitScale : 1; return new Vec(-v.X*s,v.Y*s,-v.Z*s); }
        private static Mat Translation(Vec v)
        {
#if KK
            return Mat.FromTranslation(v);
#else
            // AssimpNetter stores Assimp matrices by rows, unlike Numerics' TRS.
            Mat result=Mat.Identity; result.M14=v.X; result.M24=v.Y; result.M34=v.Z; return result;
#endif
        }
        internal static Scene Build(PmxModel model, bool morphs)
        {
            var scene=new Scene { RootNode=new Node("PMX_Model") };
            var nodes=new Node[model.Bones.Length];
            for (int i=0;i<nodes.Length;i++) nodes[i]=new Node(PmxModel.BoneName(i,model.Bones[i].Name));
            for (int i=0;i<nodes.Length;i++)
            {
                var b=model.Bones[i]; var pos=b.Position;
                if (b.Parent>=0) pos=PmxModel.V3.Add(pos,PmxModel.V3.Mul(model.Bones[b.Parent].Position,-1));
                nodes[i].Transform=Translation(Convert(pos,true));
                (b.Parent>=0 ? nodes[b.Parent] : scene.RootNode).Children.Add(nodes[i]);
            }
            var morphData=new Dictionary<int,PmxModel.V3>[model.Morphs.Length];
            if (morphs) for (int i=0;i<morphData.Length;i++) morphData[i]=FlattenMorph(model,i,new HashSet<int>(),morphData);
            int indexStart=0;
            for (int mi=0;mi<model.Materials.Length;mi++)
            {
                var source=model.Materials[mi];
                var material=new Material { Name="PMX_M"+mi.ToString("D3")+"_"+source.Name,
                    ColorDiffuse=new Colour(source.Diffuse[0],source.Diffuse[1],source.Diffuse[2],source.Diffuse[3]), IsTwoSided=source.TwoSided };
                if (source.Texture>=0) material.TextureDiffuse=new TextureSlot(model.Textures[source.Texture],TextureType.Diffuse,0,TextureMapping.FromUV,0,1,TextureOperation.Multiply,TextureWrapMode.Wrap,TextureWrapMode.Wrap,0);
                scene.Materials.Add(material);
                var mesh=new Mesh("PMX_Mesh"+mi.ToString("D3"),PrimitiveType.Triangle) { MaterialIndex=mi };
                var originals=new List<int>(); var localIndices=new Dictionary<int,int>(); var influences=new Dictionary<int,List<VertexWeight>>();
                for (int f=0;f<source.IndexCount;f+=3)
                {
                    var face=new Face();
                    // ProcessMeshes reverses Assimp faces on the final Unity mesh.
                    for (int k=2;k>=0;k--)
                    {
                        int original=model.Indices[indexStart+f+k],local;
                        if (!localIndices.TryGetValue(original,out local))
                        {
                            local=originals.Count; originals.Add(original); localIndices.Add(original,local);
                            var v=model.Vertices[original]; mesh.Vertices.Add(Convert(v.Position,true)); mesh.Normals.Add(Convert(v.Normal,false));
                            mesh.TextureCoordinateChannels[0].Add(new Vec(v.UV.X,1-v.UV.Y,0));
                            for (int w=0;w<v.Bones.Length;w++)
                            {
                                if (v.Weights[w]<=0) continue;
                                List<VertexWeight> weights;
                                if (!influences.TryGetValue(v.Bones[w],out weights)) { weights=new List<VertexWeight>(); influences.Add(v.Bones[w],weights); }
                                weights.Add(new VertexWeight(local,v.Weights[w]));
                            }
                        }
                        face.Indices.Add(local);
                    }
                    mesh.Faces.Add(face);
                }
                indexStart+=source.IndexCount; mesh.UVComponentCount[0]=2;
                // Include only effective skin influences in each renderer. Unweighted
                // controller bones still live in the node tree and the import UI.
                var keys=new List<int>(influences.Keys); keys.Sort();
                foreach (int index in keys)
                {
                    var bone=new Bone { Name=nodes[index].Name, OffsetMatrix=Translation(Convert(PmxModel.V3.Mul(model.Bones[index].Position,-1),true)) };
                    bone.VertexWeights.AddRange(influences[index]); mesh.Bones.Add(bone);
                }
                if (morphs) for (int i=0;i<morphData.Length;i++)
                {
                    var deltas=morphData[i]; if (deltas==null || deltas.Count==0) continue;
                    bool relevant=false; foreach (int original in originals) if (deltas.ContainsKey(original)) { relevant=true; break; }
                    if (!relevant) continue;
                    var attachment=new MeshAnimationAttachment { Name=model.Morphs[i].Name, Weight=1 };
                    foreach (int original in originals)
                    {
                        PmxModel.V3 delta; deltas.TryGetValue(original,out delta);
                        attachment.Vertices.Add(Convert(PmxModel.V3.Add(model.Vertices[original].Position,delta),true));
                    }
                    mesh.MeshAnimationAttachments.Add(attachment);
                }
                if (mesh.VertexCount==0) continue;
                var node=new Node("PMX_Part"+mi.ToString("D3")); node.MeshIndices.Add(scene.Meshes.Count);
                scene.Meshes.Add(mesh); scene.RootNode.Children.Add(node);
            }
            return scene;
        }
        private static Dictionary<int,PmxModel.V3> FlattenMorph(PmxModel model,int index,HashSet<int> visiting,Dictionary<int,PmxModel.V3>[] cache)
        {
            if (cache[index]!=null) return cache[index];
            if (!visiting.Add(index)) throw new System.FormatException("PMX: cyclic group morph");
            var morph=model.Morphs[index]; Dictionary<int,PmxModel.V3> result=null;
            if (morph.Type==1) result=morph.Deltas;
            else if (morph.Type==0)
            {
                result=new Dictionary<int,PmxModel.V3>();
                foreach (var group in morph.Groups)
                {
                    if (group.Value==0) continue;
                    var child=FlattenMorph(model,group.Key,visiting,cache);
                    // Do not silently import only one part of a mixed morph.
                    if (child==null) { result=null; break; }
                    foreach (var delta in child)
                    {
                        PmxModel.V3 old; result.TryGetValue(delta.Key,out old);
                        result[delta.Key]=PmxModel.V3.Add(old,PmxModel.V3.Mul(delta.Value,group.Value));
                    }
                }
            }
            visiting.Remove(index); cache[index]=result; return result;
        }
    }
}
