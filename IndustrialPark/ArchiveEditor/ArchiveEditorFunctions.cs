﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using HipHopFile;
using Newtonsoft.Json;
using static HipHopFile.Functions;

namespace IndustrialPark
{
    public partial class ArchiveEditorFunctions
    {
        public static HashSet<IRenderableAsset> renderableAssets = new HashSet<IRenderableAsset>();
        public static HashSet<AssetJSP> renderableJSPs = new HashSet<AssetJSP>();
        public static Dictionary<uint, IAssetWithModel> renderingDictionary = new Dictionary<uint, IAssetWithModel>();
        public static Dictionary<uint, string> nameDictionary = new Dictionary<uint, string>();

        public static void AddToRenderingDictionary(uint key, IAssetWithModel value)
        {
            renderingDictionary[key] = value;
        }

        public static void AddToNameDictionary(uint key, string value)
        {
            nameDictionary[key] = value;
        }

        private AutoCompleteStringCollection autoCompleteSource = new AutoCompleteStringCollection();

        public void SetTextboxForAutocomplete(TextBox textBoxFindAsset)
        {
            textBoxFindAsset.AutoCompleteCustomSource = autoCompleteSource;
        }

        public bool UnsavedChanges { get; set; } = false;
        public string currentlyOpenFilePath { get; private set; }
        public bool IsNull => hipFile == null;

        protected HipFile hipFile;
        protected Dictionary<uint, Asset> assetDictionary = new Dictionary<uint, Asset>();

        public Game game => hipFile.game;
        public Platform platform => hipFile.platform;
        protected Section_DICT DICT => hipFile.DICT;

        public bool standalone;

        public bool New()
        {
            var (hipFile, addDefaultAssets) = NewArchive.GetNewArchive();

            if (hipFile != null)
            {
                Dispose();

                currentlySelectedAssets = new List<Asset>();
                currentlyOpenFilePath = null;
                assetDictionary.Clear();

                this.hipFile = hipFile;

                if (platform == Platform.Unknown)
                    new ChoosePlatformDialog().ShowDialog();

                foreach (Section_AHDR AHDR in DICT.ATOC.AHDRList)
                    AddAssetToDictionary(AHDR, true);

                if (addDefaultAssets)
                    PlaceDefaultAssets();

                UnsavedChanges = true;
                RecalculateAllMatrices();

                return true;
            }

            return false;
        }

        public void OpenFile(string fileName, bool displayProgressBar, Platform platform, bool skipTexturesAndModels = false)
        {
            allowRender = false;

            Dispose();

            ProgressBar progressBar = new ProgressBar("Opening Archive");

            if (displayProgressBar)
                progressBar.Show();

            assetDictionary = new Dictionary<uint, Asset>();

            currentlySelectedAssets = new List<Asset>();
            currentlyOpenFilePath = fileName;

            try
            {
                hipFile = new HipFile(fileName);
            }
            catch (Exception e)
            {
                progressBar.Close();
                throw e;
            }

            progressBar.SetProgressBar(0, DICT.ATOC.AHDRList.Count, 1);

            if (this.platform == Platform.Unknown)
                hipFile.platform = platform;
            while (this.platform == Platform.Unknown)
                hipFile.platform = ChoosePlatformDialog.GetPlatform();

            string assetsWithError = "";

            List<string> autoComplete = new List<string>(DICT.ATOC.AHDRList.Count);

            foreach (Section_AHDR AHDR in DICT.ATOC.AHDRList)
            {
                string error = AddAssetToDictionary(AHDR, true, skipTexturesAndModels || standalone, false);

                if (error != null)
                    assetsWithError += error + "\n";

                autoComplete.Add(AHDR.ADBG.assetName);

                progressBar.PerformStep();
            }

            if (assetsWithError != "")
                MessageBox.Show("There was an error loading the following assets and editing has been disabled for them:\n" + assetsWithError);

            autoCompleteSource.AddRange(autoComplete.ToArray());

            if (!(skipTexturesAndModels || standalone) && ContainsAssetWithType(AssetType.RWTX))
                SetupTextureDisplay();

            RecalculateAllMatrices();

            if (!skipTexturesAndModels && ContainsAssetWithType(AssetType.PIPT) && ContainsAssetWithType(AssetType.MODL))
                foreach (var asset in assetDictionary.Values)
                    if (asset is AssetPIPT PIPT)
                        PIPT.UpdateDictionary();

            progressBar.Close();

            allowRender = true;
        }

        public void Save(string path)
        {
            currentlyOpenFilePath = path;
            Save();
        }

        public void Save()
        {
            File.WriteAllBytes(currentlyOpenFilePath, hipFile.ToBytes());
            UnsavedChanges = false;
        }

        public bool EditPack(out List<uint> unsupported)
        {
            Platform previousPlatform = platform;
            Game previousGame = game;

            var (PACK, newPlatform, newGame) = NewArchive.GetExistingArchive(platform, game, hipFile.PACK.PCRT.fileDate, hipFile.PACK.PCRT.dateString);

            unsupported = new List<uint>();

            if (PACK != null)
            {
                hipFile.PACK = PACK;

                hipFile.platform = newPlatform;
                hipFile.game = newGame;

                if (platform == Platform.Unknown)
                    new ChoosePlatformDialog().ShowDialog();

                for (int i = 0; i < internalEditors.Count; i++)
                {
                    internalEditors[i].Close();
                    i--;
                }

                if (previousPlatform != platform || previousGame != game)
                    ConvertAllAssetTypes(previousPlatform, previousGame, out unsupported);

                UnsavedChanges = true;

                return true;
            }

            return false;
        }

        public int LayerCount => DICT.LTOC.LHDRList.Count;

        public int GetLayerType(int index) => DICT.LTOC.LHDRList[index].layerType;

        public void SetLayerType(int index, int type) => DICT.LTOC.LHDRList[index].layerType = type;

        public string LayerToString(int index) => "Layer " + index.ToString("D2") + ": "
            + (game == Game.Incredibles ?
            ((LayerType_TSSM)DICT.LTOC.LHDRList[index].layerType).ToString() :
            ((LayerType_BFBB)DICT.LTOC.LHDRList[index].layerType).ToString())
            + " [" + DICT.LTOC.LHDRList[index].assetIDlist.Count() + "]";

        public List<uint> GetAssetIDsOnLayer(int index) => DICT.LTOC.LHDRList[index].assetIDlist;

        public List<Section_AHDR> GetAHDRsOfType(AssetType assetType) => (from asset in assetDictionary.Values where asset.AHDR.assetType == assetType select asset.AHDR).ToList();

        public void AddLayer(int layerType = 0)
        {
            DICT.LTOC.LHDRList.Add(new Section_LHDR()
            {
                layerType = layerType,
                assetIDlist = new List<uint>(),
                LDBG = new Section_LDBG(-1)
            });

            UnsavedChanges = true;
        }

        public void RemoveLayer(int index)
        {
            foreach (uint u in DICT.LTOC.LHDRList[index].assetIDlist.ToArray())
                RemoveAsset(u);

            DICT.LTOC.LHDRList.RemoveAt(index);

            UnsavedChanges = true;
        }

        public void MoveLayerUp(int index)
        {
            if (index > 0)
            {
                Section_LHDR previous = DICT.LTOC.LHDRList[index - 1];
                DICT.LTOC.LHDRList[index - 1] = DICT.LTOC.LHDRList[index];
                DICT.LTOC.LHDRList[index] = previous;
                UnsavedChanges = true;
            }
        }

        public void MoveLayerDown(int index)
        {
            if (index < DICT.LTOC.LHDRList.Count - 1)
            {
                Section_LHDR post = DICT.LTOC.LHDRList[index + 1];
                DICT.LTOC.LHDRList[index + 1] = DICT.LTOC.LHDRList[index];
                DICT.LTOC.LHDRList[index] = post;
                UnsavedChanges = true;
            }
        }

        public int GetLayerFromAssetID(uint assetID)
        {
            for (int i = 0; i < DICT.LTOC.LHDRList.Count; i++)
                if (DICT.LTOC.LHDRList[i].assetIDlist.Contains(assetID))
                    return i;

            throw new Exception($"Asset ID {assetID:X8} is not present in any layer.");
        }

        public void Dispose(bool showProgress = true)
        {
            autoCompleteSource.Clear();

            List<uint> assetList = new List<uint>();
            assetList.AddRange(assetDictionary.Keys);

            if (assetList.Count == 0)
                return;

            ProgressBar progressBar = new ProgressBar("Closing Archive");
            if (showProgress)
                progressBar.Show();
            progressBar.SetProgressBar(0, assetList.Count, 1);

            foreach (uint assetID in assetList)
            {
                DisposeOfAsset(assetID);
                progressBar.PerformStep();
            }

            hipFile = null;
            currentlyOpenFilePath = null;

            progressBar.Close();
        }

        public void DisposeOfAsset(uint assetID)
        {
            var asset = assetDictionary[assetID];
            currentlySelectedAssets.Remove(asset);
            CloseInternalEditor(assetID);
            CloseInternalEditorMulti(assetID);

            renderingDictionary.Remove(assetID);

            if (asset is IRenderableAsset ra)
            {
                renderableAssets.Remove(ra);
                if (renderableJSPs.Contains(ra))
                    renderableJSPs.Remove((AssetJSP)ra);
                Program.MainForm.renderer.renderableAssets.Remove(ra);
            }

            if (asset is AssetRenderWareModel jsp && jsp.HasRenderWareModelFile())
                jsp.GetRenderWareModelFile().Dispose();
            else if (asset is IAssetWithModel iawm)
                iawm.MovieRemoveFromDictionary();
            else if (asset is AssetPICK pick)
                pick.ClearDictionary();
            else if (asset is AssetTPIK tpik)
                tpik.ClearDictionary();
            else if (asset is AssetLODT lodt)
                lodt.ClearDictionary();
            else if (asset is AssetPIPT pipt)
                pipt.ClearDictionary();
            else if (asset is AssetSPLN spln)
                spln.Dispose();
            else if (asset is AssetWIRE wire)
                wire.Dispose();
            else if (asset is AssetRWTX rwtx)
                TextureManager.RemoveTexture(rwtx.Name);
        }

        public bool ContainsAsset(uint key)
        {
            return assetDictionary.ContainsKey(key);
        }

        public List<AssetType> AssetTypesOnLayer(int index) =>
            (from uint i in DICT.LTOC.LHDRList[index].assetIDlist select assetDictionary[i].AHDR.assetType).Distinct().OrderBy(f => f).ToList();

        public bool ContainsAssetWithType(AssetType assetType)
        {
            foreach (Asset a in assetDictionary.Values)
                if (a.AHDR.assetType == assetType)
                    return true;
            return false;
        }

        public Asset GetFromAssetID(uint key)
        {
            if (ContainsAsset(key))
                return assetDictionary[key];
            throw new KeyNotFoundException("Asset not present in dictionary.");
        }

        public Dictionary<uint, Asset>.ValueCollection GetAllAssets()
        {
            return assetDictionary.Values;
        }

        public int AssetCount => assetDictionary.Values.Count;

        public static bool allowRender = true;

        private string AddAssetToDictionary(Section_AHDR AHDR, bool fast, bool skipTexturesAndModels = false, bool showMessageBox = true)
        {
            allowRender = false;

            if (assetDictionary.ContainsKey(AHDR.assetID))
            {
                assetDictionary.Remove(AHDR.assetID);
                MessageBox.Show("Duplicate asset ID found: " + AHDR.assetID.ToString("X8"));
            }

            Asset newAsset;
            string error = null;

            //#if !DEBUG
            try
            {
            //#endif
                switch (AHDR.assetType)
                {
                    case AssetType.ANIM: newAsset = AHDR.ADBG.assetName.Contains("ATBL") ? new Asset(AHDR, game, platform) : newAsset = new AssetANIM(AHDR, game, platform); break;
                    case AssetType.ALST: newAsset = new AssetALST(AHDR, game, platform); break;
                    case AssetType.ATBL: newAsset = game == Game.Scooby ? new Asset(AHDR, game, platform) : new AssetATBL(AHDR, game, platform); break;
                    case AssetType.BSP: case AssetType.JSP:
                        if (DICT.LTOC.LHDRList[GetLayerFromAssetID(AHDR.assetID)].layerType > 9)
                            newAsset = new AssetJSP_INFO(AHDR, game, platform);
                        else
                            newAsset = skipTexturesAndModels ? new Asset(AHDR, game, platform) : new AssetJSP(AHDR, game, platform, Program.MainForm.renderer);
                        break;
                    case AssetType.BOUL: newAsset = new AssetBOUL(AHDR, game, platform); break;
                    case AssetType.BUTN: newAsset = new AssetBUTN(AHDR, game, platform); break;
                    case AssetType.CAM:  newAsset = new AssetCAM (AHDR, game, platform); break;
                    case AssetType.CNTR: newAsset = new AssetCNTR(AHDR, game, platform); break;
                    case AssetType.COLL: newAsset = new AssetCOLL(AHDR, game, platform); break;
                    case AssetType.COND: newAsset = new AssetCOND(AHDR, game, platform); break;
                    case AssetType.CRDT: newAsset = new AssetCRDT(AHDR, game, platform); break;
                    case AssetType.CSN:  newAsset = new AssetCSN (AHDR, game, platform); break;
                    case AssetType.CSNM: newAsset = new AssetCSNM(AHDR, game, platform); break;
                    case AssetType.DEST: newAsset = new AssetDEST(AHDR, game, platform); break;
                    case AssetType.DPAT: newAsset = new AssetDPAT(AHDR, game, platform); break;
                    case AssetType.DSCO: newAsset = new AssetDSCO(AHDR, game, platform); break;
                    case AssetType.DSTR: newAsset = new AssetDSTR(AHDR, game, platform); break;
                    case AssetType.DUPC: newAsset = new AssetDUPC(AHDR, game, platform); break;
                    case AssetType.DYNA: newAsset = new AssetDYNA(AHDR, game, platform); break;
                    case AssetType.EGEN: newAsset = new AssetEGEN(AHDR, game, platform); break;
                    case AssetType.ENV:  newAsset = new AssetENV (AHDR, game, platform); break;
                    case AssetType.FLY:  newAsset = new AssetFLY (AHDR, game, platform); break;
                    case AssetType.FOG:  newAsset = new AssetFOG (AHDR, game, platform); break;
                    case AssetType.GRUP: newAsset = new AssetGRUP(AHDR, game, platform); break;
                    case AssetType.GUST: newAsset = new AssetGUST(AHDR, game, platform); break;
                    case AssetType.HANG: newAsset = new AssetHANG(AHDR, game, platform); break;
                    case AssetType.JAW:  newAsset = new AssetJAW (AHDR, game, platform); break;
                    case AssetType.LITE: newAsset = new AssetLITE(AHDR, game, platform); break;
                    case AssetType.LKIT: newAsset = new AssetLKIT(AHDR, game, platform); break;
                    case AssetType.LOBM: newAsset = new AssetLOBM(AHDR, game, platform); break;
                    case AssetType.LODT: newAsset = new AssetLODT(AHDR, game, platform); break;
                    case AssetType.MAPR: newAsset = new AssetMAPR(AHDR, game, platform); break;
                    case AssetType.MINF: newAsset = new AssetMINF(AHDR, game, platform); break;
                    case AssetType.MODL:
                        newAsset = skipTexturesAndModels ? new Asset(AHDR, game, platform) : new AssetMODL(AHDR, game, platform, Program.MainForm.renderer); break;
                    case AssetType.MRKR: newAsset = new AssetMRKR(AHDR, game, platform); break;
                    case AssetType.MVPT: newAsset = game == Game.Scooby ? new AssetMVPT_Scooby(AHDR, game, platform) : new AssetMVPT(AHDR, game, platform); break;
                    case AssetType.NPC:  newAsset = new AssetNPC(AHDR, game, platform); break;
                    case AssetType.PARE: newAsset = new AssetPARE(AHDR, game, platform); break;
                    case AssetType.PARP: newAsset = new AssetPARP(AHDR, game, platform); break;
                    case AssetType.PARS: newAsset = new AssetPARS(AHDR, game, platform); break;
                    case AssetType.PEND: newAsset = new AssetPEND(AHDR, game, platform); break;
                    case AssetType.PICK: newAsset = new AssetPICK(AHDR, game, platform); break;
                    case AssetType.PIPT: newAsset = new AssetPIPT(AHDR, game, platform, UpdateModelBlendModes); break;
                    case AssetType.PKUP: newAsset = new AssetPKUP(AHDR, game, platform); break;
                    case AssetType.PLAT: newAsset = new AssetPLAT(AHDR, game, platform); break;
                    case AssetType.PLYR: newAsset = new AssetPLYR(AHDR, game, platform); break;
                    case AssetType.PORT: newAsset = new AssetPORT(AHDR, game, platform); break;
                    case AssetType.PRJT: newAsset = new AssetPRJT(AHDR, game, platform); break;
                    case AssetType.RWTX:
                        newAsset = skipTexturesAndModels ? new Asset(AHDR, game, platform) : new AssetRWTX(AHDR, game, platform); break;
                    case AssetType.SCRP: newAsset = new AssetSCRP(AHDR, game, platform); break;
                    case AssetType.SDFX: newAsset = new AssetSDFX(AHDR, game, platform); break;
                    case AssetType.SFX:  newAsset = new AssetSFX(AHDR, game, platform); break;
                    case AssetType.SGRP: newAsset = new AssetSGRP(AHDR, game, platform); break;
                    case AssetType.TRCK: case AssetType.SIMP: newAsset = new AssetSIMP(AHDR, game, platform); break;
                    case AssetType.SHDW: newAsset = new AssetSHDW(AHDR, game, platform); break;
                    case AssetType.SHRP: newAsset = new AssetSHRP(AHDR, game, platform); break;
                    case AssetType.SNDI:
                        if (platform == Platform.GameCube && (game == Game.BFBB || game == Game.Scooby))
                            newAsset = new AssetSNDI_GCN_V1(AHDR, game, platform);
                        else if (platform == Platform.GameCube)
                            newAsset = new AssetSNDI_GCN_V2(AHDR, game, platform);
                        else if (platform == Platform.Xbox)
                            newAsset = new AssetSNDI_XBOX(AHDR, game, platform);
                        else if (platform == Platform.PS2)
                            newAsset = new AssetSNDI_PS2(AHDR, game, platform);
                        else
                            newAsset = new Asset(AHDR, game, platform);
                        break;
                    case AssetType.SPLN:
                    newAsset = skipTexturesAndModels ? new Asset(AHDR, game, platform) : new AssetSPLN(AHDR, game, platform, Program.MainForm.renderer); break; 
                    case AssetType.SURF: newAsset = new AssetSURF(AHDR, game, platform); break;
                    case AssetType.TEXT: newAsset = new AssetTEXT(AHDR, game, platform); break;
                    case AssetType.TRIG: newAsset = new AssetTRIG(AHDR, game, platform); break;
                    case AssetType.TIMR: newAsset = new AssetTIMR(AHDR, game, platform); break;
                    case AssetType.TPIK: newAsset = new AssetTPIK(AHDR, game, platform); break;
                    case AssetType.UI:   newAsset = new AssetUI  (AHDR, game, platform); break;
                    case AssetType.UIFT: newAsset = new AssetUIFT(AHDR, game, platform); break;
                    case AssetType.VIL:  newAsset = new AssetVIL (AHDR, game, platform); break;
                    case AssetType.VILP: newAsset = new AssetVILP(AHDR, game, platform); break;
                    case AssetType.VOLU: newAsset = new AssetVOLU(AHDR, game, platform); break;
                    case AssetType.WIRE: newAsset = new AssetWIRE(AHDR, game, platform, Program.MainForm.renderer); break;
                    case AssetType.CCRV:
                    case AssetType.DTRK:
                    case AssetType.GRSM:
                    case AssetType.NGMS:
                    case AssetType.PGRS:
                    case AssetType.RANM:
                    case AssetType.SLID:
                    case AssetType.SSET:
                    case AssetType.SUBT:
                    case AssetType.TRWT:
                    case AssetType.UIM:
                    case AssetType.ZLIN:
                        newAsset = new BaseAsset(AHDR, game, platform);
                        break;
                    case AssetType.ATKT:
                    case AssetType.BINK:
                    case AssetType.CSSS:
                    case AssetType.CTOC:
                    case AssetType.MPHT:
                    case AssetType.NPCS:
                    case AssetType.ONEL:
                    case AssetType.RAW:
                    case AssetType.SND:
                    case AssetType.SNDS:
                    case AssetType.SPLP:
                    case AssetType.TEXS:
                    case AssetType.UIFN:
                        newAsset = new Asset(AHDR, game, platform);
                        break;
                    default:
                        throw new Exception($"Unknown asset type ({AHDR.assetType})");
                }
            //#if !DEBUG
            }
            catch (Exception ex)
            {
                error = $"[{ AHDR.assetID:X8}] {AHDR.ADBG.assetName}";

                if (showMessageBox)
                    MessageBox.Show($"There was an error loading asset {error}:" + ex.Message + " and editing has been disabled for it.");
                
                newAsset = new Asset(AHDR, game, platform);
            }
            //#endif

            assetDictionary[AHDR.assetID] = newAsset;

            if (hiddenAssets.Contains(AHDR.assetID))
                assetDictionary[AHDR.assetID].isInvisible = true;

            if (!fast)
                autoCompleteSource.Add(AHDR.ADBG.assetName);

            allowRender = true;

            return error;
        }

        public uint? CreateNewAsset(int layerIndex)
        {
            Section_AHDR AHDR = AssetHeader.GetAsset(new AssetHeader());
            
            if (AHDR != null)
            {
#if !DEBUG
                try
                {
#endif
                    while (ContainsAsset(AHDR.assetID))
                        MessageBox.Show($"Archive already contains asset id [{AHDR.assetID:X8}]. Will change it to [{++AHDR.assetID:X8}].");
                    
                    UnsavedChanges = true;
                    AddAsset(layerIndex, AHDR, true);
                    SetAssetPositionToView(AHDR.assetID);
#if !DEBUG
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to add asset: " + ex.Message);
                    return null;
                }
#endif
                return AHDR.assetID;
            }

            return null;
        }

        public uint AddAsset(int layerIndex, Section_AHDR AHDR, bool setTextureDisplay)
        {
            DICT.LTOC.LHDRList[layerIndex].assetIDlist.Add(AHDR.assetID);
            DICT.ATOC.AHDRList.Add(AHDR);
            AddAssetToDictionary(AHDR, false);

            if (setTextureDisplay && GetFromAssetID(AHDR.assetID) is AssetRWTX rwtx)
                EnableTextureForDisplay(rwtx);

            return AHDR.assetID;
        }

        public uint AddAssetWithUniqueID(int layerIndex, Section_AHDR AHDR, bool giveIDregardless = false, bool setTextureDisplay = false, bool ignoreNumber = false)
        {
            int numCopies = 0;
            char stringToAdd = '_';

            while (ContainsAsset(AHDR.assetID) || giveIDregardless)
            {
                if (numCopies > 1000)
                {
                    MessageBox.Show("Something went wrong: the asset you're trying to duplicate, paste or create a template of's name is too long. Due to that, I'll have to give it a new name myself.");
                    numCopies = 0;
                    AHDR.ADBG.assetName = AHDR.assetType.ToString();
                }

                giveIDregardless = false;
                numCopies++;

                if (!ignoreNumber)
                    AHDR.ADBG.assetName = FindNewAssetName(AHDR.ADBG.assetName, stringToAdd, numCopies);

                AHDR.assetID = BKDRHash(AHDR.ADBG.assetName);
            }

            return AddAsset(layerIndex, AHDR, setTextureDisplay);
        }

        public string FindNewAssetName(string previousName, char stringToAdd, int numCopies)
        {
            if (previousName.Contains(stringToAdd))
                try
                {
                    int a = Convert.ToInt32(previousName.Split(stringToAdd).Last());
                    previousName = previousName.Substring(0, previousName.LastIndexOf(stringToAdd));
                }
                catch { }

            previousName += stringToAdd + numCopies.ToString("D2");
            return previousName;
        }

        public void RemoveAsset(IEnumerable<uint> assetIDs)
        {
            foreach (uint u in assetIDs)
                RemoveAsset(u);
        }

        public void RemoveAsset(uint assetID, bool removeSound = true)
        {
            DisposeOfAsset(assetID);
            autoCompleteSource.Remove(assetDictionary[assetID].AHDR.ADBG.assetName);

            for (int i = 0; i < DICT.LTOC.LHDRList.Count; i++)
                DICT.LTOC.LHDRList[i].assetIDlist.Remove(assetID);

            if (removeSound && (GetFromAssetID(assetID).AHDR.assetType == AssetType.SND || GetFromAssetID(assetID).AHDR.assetType == AssetType.SNDS))
                RemoveSoundFromSNDI(assetID);

            DICT.ATOC.AHDRList.Remove(assetDictionary[assetID].AHDR);

            assetDictionary.Remove(assetID);
        }

        public void DuplicateSelectedAssets(int layerIndex, out List<uint> finalIndices)
        {
            UnsavedChanges = true;

            finalIndices = new List<uint>();
            Dictionary<uint, uint> referenceUpdate = new Dictionary<uint, uint>();
            var newAHDRs = new List<Section_AHDR>();

            foreach (Asset asset in currentlySelectedAssets)
            {
                string serializedObject = JsonConvert.SerializeObject(asset.AHDR);
                Section_AHDR AHDR = JsonConvert.DeserializeObject<Section_AHDR>(serializedObject);

                var previousAssetID = AHDR.assetID;

                AddAssetWithUniqueID(layerIndex, AHDR);

                referenceUpdate.Add(previousAssetID, AHDR.assetID);

                finalIndices.Add(AHDR.assetID);
                newAHDRs.Add(AHDR);
            }

            if (updateReferencesOnCopy)
                UpdateReferencesOnCopy(referenceUpdate, newAHDRs);
        }

        public void CopyAssetsToClipboard()
        {
            List<Section_AHDR> copiedAHDRs = new List<Section_AHDR>();

            foreach (Asset asset in currentlySelectedAssets)
            {
                Section_AHDR AHDR = JsonConvert.DeserializeObject<Section_AHDR>(JsonConvert.SerializeObject(asset.AHDR));

                if (AHDR.assetType == AssetType.SND || AHDR.assetType == AssetType.SNDS)
                {
                    try
                    {
                        AHDR.data = GetSoundData(AHDR.assetID, AHDR.data);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message + " The asset will be copied as it is.");
                    }
                }

                copiedAHDRs.Add(AHDR);
            }

            Clipboard.SetText(JsonConvert.SerializeObject(new AssetClipboard(game, EndianConverter.PlatformEndianness(platform), copiedAHDRs), Formatting.None));
        }

        public static bool updateReferencesOnCopy = true;
        public static bool replaceAssetsOnPaste = false;
        
        public void PasteAssetsFromClipboard(int layerIndex, out List<uint> finalIndices, AssetClipboard clipboard = null, bool forceRefUpdate = false, bool dontReplace = false)
        {
            finalIndices = new List<uint>();

            try
            {
                if (clipboard == null)
                    clipboard = JsonConvert.DeserializeObject<AssetClipboard>(Clipboard.GetText());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error pasting assets from clipboard: " + ex.Message + ". Are you sure you have assets copied?");
                return;
            }

            UnsavedChanges = true;

            Dictionary<uint, uint> referenceUpdate = new Dictionary<uint, uint>();

            foreach (Section_AHDR section in clipboard.assets)
            {
                Section_AHDR AHDR;

                try
                {
                    AHDR = ConvertAssetType(section, clipboard.endianness, EndianConverter.PlatformEndianness(platform), clipboard.game, game);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message + " The asset will be pasted without conversion.");
                    AHDR = section;
                }

                uint previousAssetID = AHDR.assetID;

                if (replaceAssetsOnPaste && !dontReplace && ContainsAsset(AHDR.assetID))
                    RemoveAsset(AHDR.assetID);

                AddAssetWithUniqueID(layerIndex, AHDR);

                referenceUpdate.Add(previousAssetID, AHDR.assetID);

                if (AHDR.assetType == AssetType.SND || AHDR.assetType == AssetType.SNDS)
                {
                    try
                    {
                        AddSoundToSNDI(AHDR.data, AHDR.assetID, AHDR.assetType, out byte[] soundData);
                        AHDR.data = soundData;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }

                finalIndices.Add(AHDR.assetID);
            }

            if (updateReferencesOnCopy || forceRefUpdate)
                UpdateReferencesOnCopy(referenceUpdate, clipboard.assets);
        }

        public void UpdateReferencesOnCopy(Dictionary<uint, uint> referenceUpdate, List<Section_AHDR> assets)
        {
            AssetType[] dontUpdate = new AssetType[] {
                    AssetType.BSP,
                    AssetType.JSP,
                    AssetType.MODL,
                    AssetType.RWTX,
                    AssetType.SND,
                    AssetType.SNDI,
                    AssetType.SNDS,
                    AssetType.TEXT
                };

            Dictionary<uint, uint> newReferenceUpdate;

            if (EndianConverter.PlatformEndianness(platform) == Endianness.Big)
            {
                newReferenceUpdate = new Dictionary<uint, uint>();
                foreach (var key in referenceUpdate.Keys)
                {
                    newReferenceUpdate.Add(
                        BitConverter.ToUInt32(BitConverter.GetBytes(key).Reverse().ToArray(), 0),
                        BitConverter.ToUInt32(BitConverter.GetBytes(referenceUpdate[key]).Reverse().ToArray(), 0));
                }
            }
            else
                newReferenceUpdate = referenceUpdate;

            foreach (Section_AHDR section in assets)
                if (!dontUpdate.Contains(section.assetType))
                    section.data = ReplaceReferences(section.data, newReferenceUpdate);
        }

        public List<uint> ImportMultipleAssets(int layerIndex, List<Section_AHDR> AHDRs, bool overwrite)
        {
            UnsavedChanges = true;
            var assetIDs = new List<uint>();

            foreach (Section_AHDR AHDR in AHDRs)
            {
                try
                {
                    if (overwrite)
                    {
                        if (ContainsAsset(AHDR.assetID))
                            RemoveAsset(AHDR.assetID);
                        AddAsset(layerIndex, AHDR, setTextureDisplay: false);
                    }
                    else
                        AddAssetWithUniqueID(layerIndex, AHDR, setTextureDisplay: true);

                    if (AHDR.assetType == AssetType.SND || AHDR.assetType == AssetType.SNDS)
                    {
                        try
                        {
                            AddSoundToSNDI(AHDR.data, AHDR.assetID, AHDR.assetType, out byte[] soundData);
                            AHDR.data = soundData;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }

                    assetIDs.Add(AHDR.assetID);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Unable to import asset [{AHDR.assetID:X8}] {AHDR.ADBG.assetName}: " + ex.Message);
                }
            }

            return assetIDs;
        }

        private List<Asset> currentlySelectedAssets = new List<Asset>();

        private static List<Asset> allCurrentlySelectedAssets
        {
            get
            {
                List<Asset> currentlySelectedAssets = new List<Asset>();
                foreach (ArchiveEditor ae in Program.MainForm.archiveEditors)
                    currentlySelectedAssets.AddRange(ae.archive.currentlySelectedAssets);
                return currentlySelectedAssets;
            }
        }

        public void SelectAssets(List<uint> assetIDs)
        {
            ClearSelectedAssets();

            foreach (uint assetID in assetIDs)
            {
                if (!assetDictionary.ContainsKey(assetID))
                    continue;

                assetDictionary[assetID].isSelected = true;
                currentlySelectedAssets.Add(assetDictionary[assetID]);
            }
        }

        public List<uint> GetCurrentlySelectedAssetIDs()
        {
            List<uint> selectedAssetIDs = new List<uint>();
            foreach (Asset a in currentlySelectedAssets)
                selectedAssetIDs.Add(a.AHDR.assetID);

            return selectedAssetIDs;
        }

        public void ClearSelectedAssets()
        {
            for (int i = 0; i < currentlySelectedAssets.Count; i++)
                currentlySelectedAssets[i].isSelected = false;

            currentlySelectedAssets.Clear();
        }

        public void ResetModels(SharpRenderer renderer)
        {
            foreach (Asset a in assetDictionary.Values)
                if (a is AssetRenderWareModel model)
                    model.Setup(renderer);
        }

        public void RecalculateAllMatrices()
        {
            foreach (IRenderableAsset a in renderableAssets)
                a.CreateTransformMatrix();
            foreach (AssetJSP a in renderableJSPs)
                a.CreateTransformMatrix();
        }
        
        public void UpdateModelBlendModes(Dictionary<uint, (int, BlendFactorType, BlendFactorType)[]> blendModes)
        {
            foreach (var asset in assetDictionary.Values)
                if (asset is AssetMODL MODL)
                    MODL.ResetBlendModes();

            if (blendModes != null)
            {
                foreach (var k in blendModes.Keys)
                    if (renderingDictionary.ContainsKey(k) && renderingDictionary[k] is AssetMODL MODL)
                        MODL.SetBlendModes(blendModes[k]);
            }
        }
    }
}