using Microsoft.AspNetCore.Mvc;
using CodeWalker.GameFiles;
using System.Text.Json;

namespace FC_YMT_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class YmtController : ControllerBase
{
    /// <summary>
    /// Generates a binary RSC7 YMT file from drawable/prop data
    /// </summary>
    [HttpPost("generate")]
    public IActionResult GenerateYmt([FromBody] YmtRequest request)
    {
        try
        {
            var ymtBytes = BuildYMT(request);
            
            var filename = $"{GetPedName(request.Gender)}_{request.ProjectName}.ymt";
            
            return File(ymtBytes, "application/octet-stream", filename);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", version = "1.0.0" });
    }

    private byte[] BuildYMT(YmtRequest request)
    {
        var mb = new MetaBuilder();
        var mdb = mb.EnsureBlock(MetaName.CPedVariationInfo);
        
        var CPed = new CPedVariationInfo
        {
            bHasDrawblVariations = 1,
            bHasTexVariations = 1,
            bHasLowLODs = 0,
            bIsSuperLOD = 0
        };

        // Generate availComp array
        var availComp = new ArrayOfBytes12();
        var genAvailComp = GenerateAvailComp(request);
        availComp.SetBytes(genAvailComp);
        CPed.availComp = availComp;

        // Build components
        var components = new Dictionary<byte, CPVComponentData>();
        for (byte i = 0; i < genAvailComp.Length; i++)
        {
            if (genAvailComp[i] == 255) continue;

            var drawablesOfType = request.Drawables
                .Where(x => x.ComponentType == i && !x.IsProp)
                .ToArray();
            
            var drawables = new CPVDrawblData[drawablesOfType.Length];

            for (int d = 0; d < drawables.Length; d++)
            {
                drawables[d].propMask = (byte)(drawablesOfType[d].HasSkin ? 17 : 1);
                drawables[d].numAlternatives = 0;
                drawables[d].clothData = new CPVDrawblData__CPVClothComponentData { ownsCloth = 0 };

                var textures = new CPVTextureData[drawablesOfType[d].TextureCount];
                for (int t = 0; t < textures.Length; t++)
                {
                    textures[t].texId = (byte)(drawablesOfType[d].HasSkin ? 1 : 0);
                    textures[t].distribution = 255;
                }
                drawables[d].aTexData = mb.AddItemArrayPtr(MetaName.CPVTextureData, textures);
            }

            components[i] = new CPVComponentData
            {
                numAvailTex = (byte)drawablesOfType.Sum(y => y.TextureCount),
                aDrawblData3 = mb.AddItemArrayPtr(MetaName.CPVDrawblData, drawables)
            };
        }

        CPed.aComponentData3 = mb.AddItemArrayPtr(MetaName.CPVComponentData, components.Values.ToArray());

        // Build component infos
        var compDrawables = request.Drawables.Where(x => !x.IsProp).ToArray();
        var compInfos = new CComponentInfo[compDrawables.Length];
        var drawableIndicesPerType = new Dictionary<int, int>();

        for (int i = 0; i < compInfos.Length; i++)
        {
            var drawable = compDrawables[i];
            var typeNum = drawable.ComponentType;

            if (!drawableIndicesPerType.ContainsKey(typeNum))
                drawableIndicesPerType[typeNum] = 0;

            var sequentialIndex = drawableIndicesPerType[typeNum]++;

            compInfos[i].pedXml_audioID = JenkHash.GenHash("none");
            compInfos[i].pedXml_audioID2 = JenkHash.GenHash("none");
            compInfos[i].pedXml_expressionMods = new ArrayOfFloats5 { f0 = 0, f1 = 0, f2 = 0, f3 = 0, f4 = 0 };
            compInfos[i].flags = 0;
            compInfos[i].inclusions = 0;
            compInfos[i].exclusions = 0;
            compInfos[i].pedXml_vfxComps = GetComponentVfxCompFlag(drawable.ComponentType);
            compInfos[i].pedXml_flags = 0;
            compInfos[i].pedXml_compIdx = (byte)drawable.ComponentType;
            compInfos[i].pedXml_drawblIdx = (byte)sequentialIndex;
        }

        CPed.compInfos = mb.AddItemArrayPtr(MetaName.CComponentInfo, compInfos);

        // Build prop info
        var propDrawables = request.Drawables.Where(x => x.IsProp).ToArray();
        var propInfo = new CPedPropInfo
        {
            numAvailProps = (byte)propDrawables.Length
        };

        var props = new CPedPropMetaData[propDrawables.Length];
        for (int i = 0; i < props.Length; i++)
        {
            var prop = propDrawables[i];
            props[i].audioId = JenkHash.GenHash("none");
            props[i].expressionMods = new ArrayOfFloats5 { f0 = 0, f1 = 0, f2 = 0, f3 = 0, f4 = 0 };

            var textures = new CPedPropTexData[prop.TextureCount];
            for (int t = 0; t < textures.Length; t++)
            {
                textures[t].inclusions = 0;
                textures[t].exclusions = 0;
                textures[t].texId = (byte)t;
                textures[t].inclusionId = 0;
                textures[t].exclusionId = 0;
                textures[t].distribution = 255;
            }

            props[i].texData = mb.AddItemArrayPtr(MetaName.CPedPropTexData, textures);
            props[i].renderFlags = 0;
            props[i].propFlags = 0;
            props[i].flags = 0;
            props[i].anchorId = (byte)prop.ComponentType;
            props[i].propId = (byte)i;
            props[i].Unk_2894625425 = 0;
        }
        propInfo.aPropMetaData = mb.AddItemArrayPtr(MetaName.CPedPropMetaData, props);

        // Build anchors
        var uniqueProps = propDrawables.GroupBy(x => x.ComponentType).Select(g => g.First()).ToArray();
        var anchors = new CAnchorProps[uniqueProps.Length];
        for (int i = 0; i < anchors.Length; i++)
        {
            var propsOfType = propDrawables.Where(x => x.ComponentType == uniqueProps[i].ComponentType);
            var items = propsOfType.Select(p => (byte)p.TextureCount).ToList();
            anchors[i].props = mb.AddByteArrayPtr(items.ToArray());
            anchors[i].anchor = (eAnchorPoints)uniqueProps[i].ComponentType;
        }
        propInfo.aAnchors = mb.AddItemArrayPtr(MetaName.CAnchorProps, anchors);

        CPed.propInfo = propInfo;
        CPed.dlcName = JenkHash.GenHash(request.ProjectName);

        mb.AddItem(MetaName.CPedVariationInfo, CPed);

        // Add structure infos
        mb.AddStructureInfo(MetaName.CPedVariationInfo);
        mb.AddStructureInfo(MetaName.CPedPropInfo);
        mb.AddStructureInfo(MetaName.CPedPropTexData);
        mb.AddStructureInfo(MetaName.CAnchorProps);
        mb.AddStructureInfo(MetaName.CComponentInfo);
        mb.AddStructureInfo(MetaName.CPVComponentData);
        mb.AddStructureInfo(MetaName.CPVDrawblData);
        mb.AddStructureInfo(MetaName.CPVDrawblData__CPVClothComponentData);
        mb.AddStructureInfo(MetaName.CPVTextureData);
        mb.AddStructureInfo(MetaName.CPedPropMetaData);
        mb.AddEnumInfo(MetaName.ePedVarComp);
        mb.AddEnumInfo(MetaName.eAnchorPoints);
        mb.AddEnumInfo(MetaName.ePropRenderFlags);

        Meta meta = mb.GetMeta();
        meta.Name = request.ProjectName;

        return ResourceBuilder.Build(meta, 2);
    }

    private byte[] GenerateAvailComp(YmtRequest request)
    {
        byte[] genAvailComp = { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 };
        byte compCount = 0;

        for (int i = 0; i < genAvailComp.Length; i++)
        {
            var compExist = request.Drawables.Any(x => x.ComponentType == i && !x.IsProp);
            if (compExist)
            {
                genAvailComp[i] = compCount;
                compCount++;
            }
        }
        return genAvailComp;
    }

    private ePedVarComp GetComponentVfxCompFlag(int typeNumeric)
    {
        return typeNumeric switch
        {
            0 => ePedVarComp.PV_COMP_HEAD,
            1 => ePedVarComp.PV_COMP_BERD,
            2 => ePedVarComp.PV_COMP_HAIR,
            3 => ePedVarComp.PV_COMP_UPPR,
            4 => ePedVarComp.PV_COMP_LOWR,
            5 => ePedVarComp.PV_COMP_HAND,
            6 => ePedVarComp.PV_COMP_FEET,
            7 => ePedVarComp.PV_COMP_TEEF,
            8 => ePedVarComp.PV_COMP_ACCS,
            9 => ePedVarComp.PV_COMP_TASK,
            10 => ePedVarComp.PV_COMP_DECL,
            11 => ePedVarComp.PV_COMP_JBIB,
            _ => ePedVarComp.PV_COMP_MAX
        };
    }

    private string GetPedName(string gender)
    {
        return gender.ToLower() == "male" ? "mp_m_freemode_01" : "mp_f_freemode_01";
    }
}

public class YmtRequest
{
    public string ProjectName { get; set; } = "";
    public string Gender { get; set; } = "male";
    public List<DrawableInfo> Drawables { get; set; } = new();
}

public class DrawableInfo
{
    public int ComponentType { get; set; }
    public int TextureCount { get; set; }
    public bool IsProp { get; set; }
    public bool HasSkin { get; set; }
}
