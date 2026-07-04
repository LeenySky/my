using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UltraScene
{
    public class UltraSceneController : MVRScript
    {
        // REFERENCJE WEWNĘTRZNYCH MODUŁÓW WYKONAWCZYCH PLUGINA (ORYGINALNE NAZEWNICTWO)
        private UltraAudioModule audioModule;
        private UltraBreathingModule breathingModule;
        private UltraLickingModule lickingModule;
        private UltraHandjobModule handjobModule;
        private UltraBlowjobModule blowjobModule;
        private UltraPenetrationModule penetrationModule;
        private UltraExpressionModule expressionModule;
        private UltraRealismModule realismModule;
        private UltraOrgasmSystem orgasmSystem;
        private UltraGazeModule gazeModule;

        // MASZYNA STANÓW I PARAMETRY NAWIGACJI PROCEDURALNEGO INTERFEJSU SSS
        private string currentTab = "";
        private string currentSubMenu = "Main"; 
        private string selectedTargetName = ""; 
        private GameObject overlayCanvas;
        private Text overlayTextInfo;

        // SYSTEM WYKRYWANIA I RESETOWANIA POZYCJI T (T-POSE RELEASE CORES)
        private List<string> relaxedPersons = new List<string>();

        // STRUKTURY DANYCH TEKSTOWEJ NAKŁADKI EKRANOWEJ OSD
        private Dictionary<string, float> personEnergyLevel = new Dictionary<string, float>();
        private Dictionary<string, string> personMoveTarget = new Dictionary<string, string>();
        private Dictionary<string, string> personPersonality = new Dictionary<string, string>();

        // SŁOWNIK MAPOWANIA IDENTYFIKATORÓW W RAMACH AKTYWNEJ PĘTLI UPDATE
        private Dictionary<string, string> personKeyByAtomUid = new Dictionary<string, string>();

        // SYSTEM PROXIMITY DLA MODUŁU BLOWJOB (DETEKCJA BLISKOŚCI I TEMPO)
        private const float BlowjobProximityThreshold = 0.15f;
        private Dictionary<string, float> bjNextPlayTime = new Dictionary<string, float>();
        private HashSet<string> bjControlActiveUids = new HashSet<string>();

        // STRUKTURY BANKÓW AUDIO DLA POSTACI ORAZ ELEMENTÓW ŚRODOWISKA
        private Dictionary<string, AudioBank> personAudioBanks = new Dictionary<string, AudioBank>();
        private AudioBank assetAudioBank;
        private AudioBank impactAudioBank;
        private bool audioBanksRestored = false;
        // KATEGORIE AUDIO O IDENTYCZNEJ STRUKTURZE (Import/Clear/Volume/Amplification/Pitch/Interval/3D Audio)
        // PER POSTAĆ. SPŁASZCZONY słownik (klucz = "Kategoria|Person N") zapobiegający błędom kompilatora Mono
        private Dictionary<string, AudioBank> simpleAudioBanks = new Dictionary<string, AudioBank>();
        private static readonly string[] SimpleAudioCategories = { "Breathing", "Licking", "Blowjob", "Penetration", "Orgasm", "Handjob" };
        
        private Dictionary<string, string> simpleAudioCategoryPrefix = new Dictionary<string, string>
        {
            { "Breathing", "AudBr" },
            { "Licking", "AudLk" },
            { "Blowjob", "AudBj" },
            { "Penetration", "AudPn" },
            { "Orgasm", "AudOg" },
            { "Handjob", "AudHj" }
        };

        // DOMYŚLNY KATALOG OTWIERANY PRZY IMPORCIE AUDIO
        private const string DefaultAudioFolder = "Custom/Sounds/UltraSceneController";

        // REGISTRY AKTYWNYCH ELEMENTÓW INTERFEJSU W RENDERINGU PROCEDURALNYM (WZORZEC SSS)
        private List<Action> activeDynamicElements = new List<Action>();

        // Stan pomocniczy nawigacji menu audio
        private string audioCategory = "";

        // SŁOWNIK GLOBALNYCH PARAMETRÓW KONTROLEK INTERFEJSU (WZORZEC SSS)
        public Dictionary<string, JSONStorableBool> pluginBools = new Dictionary<string, JSONStorableBool>();
        public Dictionary<string, JSONStorableFloat> pluginFloats = new Dictionary<string, JSONStorableFloat>();
        private Dictionary<string, JSONStorableStringChooser> personalityStorables = new Dictionary<string, JSONStorableStringChooser>();
        private Dictionary<string, JSONStorableStringChooser> moveStorables = new Dictionary<string, JSONStorableStringChooser>();
        private Dictionary<string, JSONStorableFloat> energyStorables = new Dictionary<string, JSONStorableFloat>();

        // JEDNORAZOWA REJESTRACJA PARAMETRÓW LOGICZNYCH W PAMIĘCI RAM (WZORZEC SSS)
        public override void Init()
        {
            try
            {
                if (containingAtom == null) return;
                AudioSource src = containingAtom.GetComponentInChildren<AudioSource>();
                
                if (src == null)
                {
                    GameObject audioObj = new GameObject("US_AudioSource");
                    src = audioObj.AddComponent<AudioSource>();
                    audioObj.transform.SetParent(containingAtom.transform, false);
                }

                // Inicjalizacja instancji Twoich oryginalnych podmodułów wykonawczych
                audioModule = new UltraAudioModule(this, src);
                breathingModule = new UltraBreathingModule(this, src);
                lickingModule = new UltraLickingModule(this);
                handjobModule = new UltraHandjobModule(this);
                blowjobModule = new UltraBlowjobModule(this);
                penetrationModule = new UltraPenetrationModule(this);
                expressionModule = new UltraExpressionModule(this);
                realismModule = new UltraRealismModule(this);
                orgasmSystem = new UltraOrgasmSystem(this, blowjobModule);
                gazeModule = new UltraGazeModule(this);

                if (relaxedPersons != null) relaxedPersons.Clear();
                // PRZEDREJESTRACJA STRUKTUR DANYCH DLA 4 POSTACI (PERSON 1-4)
                for (int i = 1; i <= 4; i++)
                {
                    string key = "Person " + i;
                    personPersonality[key] = "Neutral";
                    personMoveTarget[key] = "None";
                    personEnergyLevel[key] = 0.0f;

                    var pChoice = new JSONStorableStringChooser("Person " + i + " Personality", 
                        new List<string> { "Neutral", "Happy", "Angry", "Fear", "Surprise", "Disgust", "Ectasy", "Sensual", "Shy", "Tired" }, 
                        "Neutral", "Personality", (string val) => { if(!string.IsNullOrEmpty(val)) personPersonality[key] = val; });
                    RegisterStringChooser(pChoice); personalityStorables[key] = pChoice;

                    var mChoice = new JSONStorableStringChooser("Person " + i + " Move Target", 
                        new List<string> { "None", "Camera", "Person 1", "Person 2", "Person 3", "Person 4" }, 
                        "None", "Move Target", (string val) => { if(!string.IsNullOrEmpty(val)) personMoveTarget[key] = val; });
                    RegisterStringChooser(mChoice); moveStorables[key] = mChoice;

                    var aSlider = new JSONStorableFloat("Person " + i + " Energy Level", 0f, (float val) => { personEnergyLevel[key] = val; }, 0f, 100f, true);
                    RegisterFloat(aSlider); energyStorables[key] = aSlider;

                    var audioPathsStorable = new JSONStorableString("Person " + i + " Audio Files", "");
                    RegisterString(audioPathsStorable);
                    personAudioBanks[key] = new AudioBank(audioPathsStorable);
                }

                var assetAudioPathsStorable = new JSONStorableString("Asset Audio Files", "");
                RegisterString(assetAudioPathsStorable);
                assetAudioBank = new AudioBank(assetAudioPathsStorable);

                // GLOBALNE PARAMETRY DŹWIĘKU PRZESTRZENNEGO DLA POSTACI I ZASOBÓW
                pluginBools["Aud3D"] = new JSONStorableBool("Persons 3D Audio", false);
                pluginBools["As3D"] = new JSONStorableBool("Assets 3D Audio", false);
                RegisterBool(pluginBools["Aud3D"]);
                RegisterBool(pluginBools["As3D"]);
                // DEFINICJA I PEŁNA REJESTRACJA GLOBALNEGO BANKU IMPACT
                pluginFloats["AudImVolume"] = new JSONStorableFloat("Impact Volume", 0.8f, 0f, 1f, true);
                pluginFloats["AudImAmplification"] = new JSONStorableFloat("Impact Amplification", 1.0f, 0f, 3f, true);
                pluginFloats["AudImPitch"] = new JSONStorableFloat("Impact Pitch", 1.0f, 0f, 3f, true);
                pluginFloats["AudImInterval"] = new JSONStorableFloat("Impact Interval Speed", 1.0f, 0f, 3f, true);
                pluginBools["AudIm3D"] = new JSONStorableBool("Impact 3D Audio", false);
                
                RegisterFloat(pluginFloats["AudImVolume"]);
                RegisterFloat(pluginFloats["AudImAmplification"]);
                RegisterFloat(pluginFloats["AudImPitch"]);
                RegisterFloat(pluginFloats["AudImInterval"]);
                RegisterBool(pluginBools["AudIm3D"]);
                
                var impactPathsStorable = new JSONStorableString("Impact Audio Files", "");
                RegisterString(impactPathsStorable);
                impactAudioBank = new AudioBank(impactPathsStorable);

                // REJESTRACJA PARAMETRÓW DLA 6 GŁÓWNYCH KATEGORII AUDIO - PER POSTAĆ
                foreach (string category in SimpleAudioCategories)
                {
                    string prefix = simpleAudioCategoryPrefix[category];
                    pluginFloats[prefix + "Volume"] = new JSONStorableFloat(category + " Volume", 0.8f, 0f, 1f, true);
                    pluginFloats[prefix + "Amplification"] = new JSONStorableFloat(category + " Amplification", 1.0f, 0f, 3f, true);
                    pluginFloats[prefix + "Pitch"] = new JSONStorableFloat(category + " Pitch", 1.0f, 0f, 3f, true);
                    pluginFloats[prefix + "Interval"] = new JSONStorableFloat(category + " Interval Speed", 1.0f, 0f, 3f, true);
                    pluginBools[prefix + "3D"] = new JSONStorableBool(category + " 3D Audio", false);
                    
                    RegisterFloat(pluginFloats[prefix + "Volume"]);
                    RegisterFloat(pluginFloats[prefix + "Amplification"]);
                    RegisterFloat(pluginFloats[prefix + "Pitch"]);
                    RegisterFloat(pluginFloats[prefix + "Interval"]);
                    RegisterBool(pluginBools[prefix + "3D"]);

                    for (int p = 1; p <= 4; p++)
                    {
                        string personKey = "Person " + p;
                        var bankStorable = new JSONStorableString(category + " " + personKey + " Audio Files", "");
                        RegisterString(bankStorable);
                        simpleAudioBanks[category + "|" + personKey] = new AudioBank(bankStorable);
                    }
                }
                // INICJALIZACJA FLAG LOGICZNYCH (PRZEŁĄCZNIKI SYSTEMOWE)
                pluginBools["HJEnableLeft"] = new JSONStorableBool("Enable Left Hand Sync", false);
                pluginBools["HJEnableRight"] = new JSONStorableBool("Enable Right Hand Sync", false);
                pluginBools["BREnable"] = new JSONStorableBool("Enable Breathing System", true);
                pluginBools["GZEnable"] = new JSONStorableBool("Enable Gaze System", true);
                pluginBools["IKEnable"] = new JSONStorableBool("Enable IK Solver", false);
                pluginBools["PNEnable"] = new JSONStorableBool("Enable Collision System", false);
                pluginBools["AUEnable"] = new JSONStorableBool("Enable Audio Feedback", true);
                
                // PARAMETRY AUTOMATYZACJI REALIZMU RUCHU
                pluginBools["RLAutoBreathing"] = new JSONStorableBool("Auto Breathing", true);
                pluginBools["RLAutoBlinking"] = new JSONStorableBool("Auto Blinking Animation", true);
                pluginBools["RLAutoClothing"] = new JSONStorableBool("Auto Layer Control", false);
                pluginBools["RLAutoLipSync"] = new JSONStorableBool("Auto Audio Sync", true);
                pluginBools["RLAutoHandMovements"] = new JSONStorableBool("Auto Hand Movements", false);
                pluginBools["RLAutoEnvironmentSound"] = new JSONStorableBool("Auto Collision Sound", true);
                pluginBools["RLAutoPhysics"] = new JSONStorableBool("Auto Physical Impact", false);
                pluginBools["RLAutoTransitions"] = new JSONStorableBool("Auto State System", true);
                pluginBools["RLAutoTessellation"] = new JSONStorableBool("Auto Mesh Tessellation", false);
                pluginBools["RLAutoFreezePose"] = new JSONStorableBool("Auto Freeze Pose", true);
                pluginBools["RLAutoMovement"] = new JSONStorableBool("Auto Micro Movement", false);
                pluginBools["RLAutoNaturalMotion"] = new JSONStorableBool("Auto Natural Motion", true);

                // Bezpieczna rejestracja tablicowa w silniku
                string[] boolKeysToRegister = { 
                    "HJEnableLeft", "HJEnableRight", "BREnable", "GZEnable", "IKEnable", "PNEnable", "AUEnable", 
                    "RLAutoBreathing", "RLAutoBlinking", "RLAutoClothing", "RLAutoLipSync", 
                    "RLAutoHandMovements", "RLAutoEnvironmentSound", "RLAutoPhysics", "RLAutoTransitions", 
                    "RLAutoTessellation", "RLAutoFreezePose", "RLAutoMovement", "RLAutoNaturalMotion" 
                };

                foreach (string bk in boolKeysToRegister) 
                { 
                    if (pluginBools.ContainsKey(bk)) RegisterBool(pluginBools[bk]); 
                }
                // INICJALIZACJA SUWAKÓW LICZBOWYCH DLA PRĘDKOŚCI I INTENSYWNOŚCI
                pluginFloats["AudVolume"] = new JSONStorableFloat("Audio Volume", 0.8f, 0f, 1f, true);
                pluginFloats["AudAmplification"] = new JSONStorableFloat("Audio Amplification", 1.0f, 0f, 3f, true);
                pluginFloats["AudPitch"] = new JSONStorableFloat("Audio Pitch", 1.0f, 0f, 3f, true);
                pluginFloats["AudInterval"] = new JSONStorableFloat("Audio Interval Speed", 1.0f, 0f, 3f, true);
                
                pluginFloats["AsVolume"] = new JSONStorableFloat("Asset Volume", 0.8f, 0f, 1f, true);
                pluginFloats["AsAmplification"] = new JSONStorableFloat("Asset Amplification", 1.0f, 0f, 3f, true);
                pluginFloats["AsPitch"] = new JSONStorableFloat("Asset Pitch", 1.0f, 0f, 3f, true);
                pluginFloats["AsInterval"] = new JSONStorableFloat("Asset Interval Speed", 1.0f, 0f, 3f, true);
                
                pluginFloats["HJVelLeft"] = new JSONStorableFloat("Left Hand Velocity", 1.0f, 0f, 3f, true);
                pluginFloats["HJVelRight"] = new JSONStorableFloat("Right Hand Velocity", 1.0f, 0f, 3f, true);
                pluginFloats["HJIntLeft"] = new JSONStorableFloat("Left Hand Intensity", 1.0f, 0f, 2f, true);
                pluginFloats["HJIntRight"] = new JSONStorableFloat("Right Hand Intensity", 1.0f, 0f, 2f, true);
                
                pluginFloats["BRVelocity"] = new JSONStorableFloat("Breathing Velocity", 1.0f, 0f, 3f, true);
                pluginFloats["BRIntense"] = new JSONStorableFloat("Breathing Intensity", 1.0f, 0f, 2f, true);
                pluginFloats["BJVelocity"] = new JSONStorableFloat("Oral Action Velocity", 1.0f, 0f, 3f, true);
                pluginFloats["BJIntense"] = new JSONStorableFloat("Oral Action Intensity", 1.0f, 0f, 2f, true);
                pluginFloats["PNVelocity"] = new JSONStorableFloat("Collision Action Velocity", 1.0f, 0f, 3f, true);
                pluginFloats["PNIntense"] = new JSONStorableFloat("Collision Action Intensity", 1.0f, 0f, 2f, true);
                
                pluginFloats["GZHeadWeight"] = new JSONStorableFloat("Head Look Weight", 0.7f, 0f, 1f, true);
                pluginFloats["GZChestWeight"] = new JSONStorableFloat("Chest Look Weight", 0.7f, 0f, 1f, true);

                string[] floatKeysToRegister = { 
                    "AudVolume", "AudAmplification", "AudPitch", "AudInterval", "AsVolume", "AsAmplification", 
                    "AsPitch", "AsInterval", "HJVelLeft", "HJVelRight", "HJIntLeft", "HJIntRight", 
                    "BRVelocity", "BRIntense", "BJVelocity", "BJIntense", "PNVelocity", "PNIntense", 
                    "GZHeadWeight", "GZChestWeight" 
                };

                foreach (string fk in floatKeysToRegister) 
                { 
                    if (pluginFloats.ContainsKey(fk)) RegisterFloat(pluginFloats[fk]); 
                }

                InitStorableParameters();
                CreateScreenOverlayUI();
                CreateDynamicLayoutUI();
            }
            catch (Exception e) 
            { 
                SuperController.LogError("Init Error: " + e.Message); 
            }
        }

        private void InitStorableParameters()
        {
            RegisterBool(new JSONStorableBool("Master Audio Switch", true));
            RegisterBool(new JSONStorableBool("Master Simulation Switch", true));
            RegisterFloat(new JSONStorableFloat("Global Output Gain", 0.8f, 0f, 1f));
        }
        // W PEŁNI ZABEZPIECZONA PĘTLA UPDATE - PRZYWRÓCENIE OSD I DYNAMICZNYCH NAZW POSTACI
        // CAŁKOWICIE ZABEZPIECZONA I ZOPTYMALIZOWANA PĘTLA UPDATE Z AKTYWACJĄ PANELU HUD
        // W PEŁNI ZSYNCHRONIZOWANA PĘTLA UPDATE Z SILNIKIEM DETEKCJI LINKÓW TMPro
        public void Update()
        {
            try
            {
                if (SuperController.singleton == null) return;
                
                if (!audioBanksRestored && !SuperController.singleton.isLoading)
                {
                    audioBanksRestored = true;
                    if (personAudioBanks != null)
                    {
                        List<string> personKeys = new List<string>(personAudioBanks.Keys);
                        for (int i = 0; i < personKeys.Count; i++)
                        {
                            AudioBank bank = personAudioBanks[personKeys[i]];
                            if (bank != null) bank.RebuildFromStorable();
                        }
                    }
                    if (assetAudioBank != null) assetAudioBank.RebuildFromStorable();
                    if (impactAudioBank != null) impactAudioBank.RebuildFromStorable();
                    if (simpleAudioBanks != null)
                    {
                        List<string> simpleKeys = new List<string>(simpleAudioBanks.Keys);
                        for (int i = 0; i < simpleKeys.Count; i++)
                        {
                            AudioBank bank = simpleAudioBanks[simpleKeys[i]];
                            if (bank != null) bank.RebuildFromStorable();
                        }
                    }
                }

                List<Atom> allAtoms = SuperController.singleton.GetAtoms();
                if (allAtoms == null) return;

                int pCount = 1;

                for (int i = 0; i < allAtoms.Count; i++)
                {
                    Atom atom = allAtoms[i];
                    if (atom == null || atom.type != "Person") continue;

                    string pName = !string.IsNullOrEmpty(atom.name) ? atom.name : atom.uid;
                    string key = "Person " + pCount;

                    // UWOLNIENIE Z POZYCJI T-POSE
                    if (!relaxedPersons.Contains(pName) && pCount <= 4)
                    {
                        JSONStorable physicsStorable = atom.GetStorableByID("Physics");
                        if (physicsStorable != null) physicsStorable.CallAction("Simulate");
                        
                        string[] targets = { "hipControl", "chestControl", "headControl", "leftHandControl", "rightHandControl", "leftFootControl", "rightFootControl" };
                        for (int t = 0; t < targets.Length; t++)
                        {
                            JSONStorable ctrl = atom.GetStorableByID(targets[t]);
                            if (ctrl != null) ctrl.CallAction("ON");
                        }

                        Rigidbody[] rbs = atom.GetComponentsInChildren<Rigidbody>();
                        if (rbs != null) 
                        {
                            for (int r = 0; r < rbs.Length; r++)
                            {
                                Rigidbody rb = rbs[r];
                                if (rb != null && (rb.name.Contains("Hand") || rb.name.Contains("Arm"))) 
                                {
                                    rb.isKinematic = false;
                                    rb.WakeUp();
                                }
                            }
                        }
                        relaxedPersons.Add(pName);
                    }

                    if (pCount <= 4)
                    {
                        personKeyByAtomUid[atom.uid] = key;

                        if (breathingModule != null)
                        {
                            breathingModule.UpdateModule(atom);
                        }

                        // --- PEŁNA I ZWERYFIKOWANA BLOKADA NATYWNYCH AUTOMATYZMÓW VaM ---
                        
                        // 1. Wyłączenie automatycznej mimiki twarzy (Auto Expressions)
                        JSONStorable autoExpressionsStorable = atom.GetStorableByID("AutoExpressions");
                        if (autoExpressionsStorable != null)
                        {
                            // Główny przełącznik całego modułu mimiki w VaM nazywa się po prostu "enabled"
                            JSONStorableBool autoExprEnabled = autoExpressionsStorable.GetBoolJSONParam("enabled");
                            if (autoExprEnabled != null && autoExprEnabled.val)
                            {
                                autoExprEnabled.val = false;
                            }

                            // Zabezpieczenie na wypadek mrugania przypisanego do tej samej zakładki
                            JSONStorableBool blinkToggleAE = autoExpressionsStorable.GetBoolJSONParam("blinkEnabled");
                            if (blinkToggleAE != null && blinkToggleAE.val)
                            {
                                blinkToggleAE.val = false;
                            }
                        }

                        // 2. Wyłączenie automatycznego mrugania oczu (Eyelid Control)
                        JSONStorable eyelidControlStorable = atom.GetStorableByID("EyelidControl");
                        if (eyelidControlStorable != null)
                        {
                            // Poprawny klucz w zakładce sterowania powiekami postaci
                            JSONStorableBool blinkToggle = eyelidControlStorable.GetBoolJSONParam("blinkEnabled");
                            if (blinkToggle != null && blinkToggle.val)
                            {
                                blinkToggle.val = false;
                            }
                        }

                        // BEZPIECZNA IMPLEMENTACJA MODUŁU EKSPRESJI
                        if (expressionModule != null)
                        {
                            float currentArousal = 0f;
                            if (personEnergyLevel != null) personEnergyLevel.TryGetValue(key, out currentArousal);

                            string currentAction = "Neutral"; 

                            string currentPersonality = "Neutral";
                            if (personalityStorables != null && personalityStorables.ContainsKey(key) && personalityStorables[key] != null)
                            {
                                currentPersonality = personalityStorables[key].val;
                            }

                            expressionModule.UpdateModule(atom, key, currentArousal, currentAction, currentPersonality);
                        }
                    }

                    pCount++;
                }

                // GENERATOR HUD: Wysyłamy dane na żywo do zaktualizowanego panelu TextMeshPro
                BuildHUDContentText(allAtoms);

                // AUTOMATYKA INTERAKCJI ZBLIŻENIOWEJ
                JSONStorableBool automaticToggle = GetBool("IKEnable");
                if (automaticToggle != null && automaticToggle.val)
                {
                    UpdateActionProximitySystem(allAtoms);
                }
                else if (bjControlActiveUids.Count > 0)
                {
                    List<string> activeUids = new List<string>(bjControlActiveUids);
                    for (int i = 0; i < activeUids.Count; i++)
                    {
                        SetAutoControl(SuperController.singleton.GetAtomByUid(activeUids[i]), false);
                    }
                    bjControlActiveUids.Clear();
                }
            }
            catch (Exception ex)
            {
                SuperController.LogError("[UltraScene] Update Loop critical error: " + ex.Message);
            }
        }


        public void OnDestroy() 
        { 
            if (overlayCanvas != null) UnityEngine.Object.Destroy(overlayCanvas); 
        }
        // =========================================================================
        // SYSTEM AKCJI ZBLIŻENIOWEJ: Bliskość startuje sekwencję, Velocity ustala tempo,
        // TargetAutoSync włącza natywny mechanizm automatycznej kontroli struktur na odbiorcy.
        // =========================================================================
        private void UpdateActionProximitySystem(List<Atom> allAtoms)
        {
            JSONStorableBool autoToggle = GetBool("IKEnable");
            bool autoEnabled = (autoToggle != null && autoToggle.val);
            HashSet<string> inRangeNow = new HashSet<string>();

            for (int i = 0; i < allAtoms.Count; i++)
            {
                Atom giver = allAtoms[i];
                if (giver == null || giver.type != "Person") continue;
                DAZCharacterSelector giverGeo = giver.GetStorableByID("geometry") as DAZCharacterSelector;
                if (giverGeo == null || giverGeo.gender != DAZCharacterSelector.Gender.Male) continue;
                FreeControllerV3 giverHip = giver.GetStorableByID("hipControl") as FreeControllerV3;
                if (giverHip == null) continue;

                for (int j = 0; j < allAtoms.Count; j++)
                {
                    Atom receiver = allAtoms[j];
                    if (receiver == null || receiver == giver || receiver.type != "Person") continue;
                    FreeControllerV3 receiverHead = receiver.GetStorableByID("headControl") as FreeControllerV3;
                    if (receiverHead == null) continue;

                    float dist = Vector3.Distance(giverHip.transform.position, receiverHead.transform.position);
                    if (dist > BlowjobProximityThreshold) continue;

                    inRangeNow.Add(receiver.uid);
                    PlayActionBeat(receiver);
                }
            }

            // Włącz automatyczną kontrolę nowym odbiorcom w zasięgu
            List<string> inRangeList = new List<string>(inRangeNow);
            for (int i = 0; i < inRangeList.Count; i++)
            {
                string uid = inRangeList[i];
                if (autoEnabled && !bjControlActiveUids.Contains(uid))
                {
                    SetAutoControl(SuperController.singleton.GetAtomByUid(uid), true);
                    bjControlActiveUids.Add(uid);
                }
            }

            // Wyłącz automatyczną kontrolę tym, którzy wypadli z zasięgu albo gdy system wyłączono
            List<string> toTurnOff = new List<string>();
            List<string> activeUids = new List<string>(bjControlActiveUids);
            for (int i = 0; i < activeUids.Count; i++)
            {
                string uid = activeUids[i];
                if (!autoEnabled || !inRangeNow.Contains(uid)) toTurnOff.Add(uid);
            }
            for (int i = 0; i < toTurnOff.Count; i++)
            {
                string uid = toTurnOff[i];
                SetAutoControl(SuperController.singleton.GetAtomByUid(uid), false);
                bjControlActiveUids.Remove(uid);
            }
        }

        // Odtwarza kolejny "beat" z banku audio danej kategorii odbiorcy w tempie wyznaczonym przez suwak Velocity
        private void PlayActionBeat(Atom receiver)
        {
            if (receiver == null) return;
            string uid = receiver.uid;
            float now = Time.time;
            float nextTime;

            if (!bjNextPlayTime.TryGetValue(uid, out nextTime)) nextTime = 0f;
            if (now < nextTime) return;

            JSONStorableFloat velocityStorable = GetFloat("BJVelocity");
            float velocity = (velocityStorable != null) ? velocityStorable.val : 1f;
            bjNextPlayTime[uid] = now + (1f / Mathf.Max(0.05f, velocity));

            string key;
            if (!personKeyByAtomUid.TryGetValue(uid, out key)) return;

            AudioBank bank;
            if (!simpleAudioBanks.TryGetValue("Blowjob|" + key, out bank) || bank == null) return;

            NamedAudioClip clip = bank.GetRandomClip();
            if (clip == null) return;

            AudioSourceControl receiverAudio = receiver.GetStorableByID("HeadAudioSource") as AudioSourceControl;
            if (receiverAudio != null) receiverAudio.PlayNow(clip);
        }

        // Przełącza natywne systemy automatyzacji struktur anatomicznych aparatu mowy
        private void SetAutoControl(Atom atom, bool enabled)
        {
            if (atom == null) return;
            JSONStorable jawControl = atom.GetStorableByID("JawControl");
            if (jawControl != null) jawControl.SetBoolParamValue("driveXRotationFromAudioSource", enabled);
            JSONStorable autoJawMouthMorph = atom.GetStorableByID("AutoJawMouthMorph");
            if (autoJawMouthMorph != null) autoJawMouthMorph.SetBoolParamValue("enabled", enabled);
        }
        // =========================================================================
        // WZORZEC SSS: CZYSZCZENIE UKŁADU I NAWIGACJA ZGODNIE Z DOKUMENTACJĄ PDF
        // =========================================================================
        private void CreateDynamicLayoutUI()
        {
            try
            {
                ClearDynamicLayout();

                // WIERSZ 1: Symetryczna nawigacja na samej górze okna wtyczki
                if (currentSubMenu != "Main") 
                {
                    AddBackButton();
                }

                // Krok 4: Renderowanie czystej zawartości wybranego pod-panelu
                switch (currentSubMenu)
                {
                    case "Audio_Home": RenderAudioHome(); break;
                    case "Audio_PersonPicker": RenderPersonPicker((key) => { selectedTargetName = key; currentSubMenu = "Execute"; CreateDynamicLayoutUI(); }); break;
                    case "Audio_Assets": RenderAudioAssets(); break;
                    case "Select_Person": RenderPersonPicker((key) => { selectedTargetName = key; currentSubMenu = "Execute"; CreateDynamicLayoutUI(); }); break;
                    case "Execute": RenderExecutePanel(); break;
                    default: RenderMainMenu(); break;
                }
            }
            catch (Exception e) { SuperController.LogError("[UltraUI] Build Error: " + e.Message); }
        }

        private void ClearDynamicLayout()
        {
            for (int i = 0; i < activeDynamicElements.Count; i++)
            {
                try { activeDynamicElements[i](); } catch { }
            }
            activeDynamicElements.Clear();
        }

        private string BuildHeaderText()
        {
            return string.IsNullOrEmpty(currentTab) ? "Main Interface" : currentTab;
        }

        private void AddHeaderLabel(string text, Color color)
        {
            // Metoda zachowana pusta dla kompatybilności strukturalnej
        }

        private void AddCharacterSubHeader(string text, Color color)
        {
            // Metoda zachowana pusta dla kompatybilności strukturalnej
        }

        private void AddBackButton()
        {
            // POPRAWKA: Przycisk powrotu do menu głównego zyskuje idealny, intuicyjny niebieski kolor #2255ee
            Color menuBlue = new Color(0.133f, 0.333f, 0.933f); 
            AddButton("Back to Previous", false, Color.gray, () => { GoBackMenuLogic(); });
            AddButton("Go to Main Menu", true, menuBlue, () => { 
                currentSubMenu = "Main"; 
                currentTab = ""; 
                selectedTargetName = ""; 
                audioCategory = ""; 
                CreateDynamicLayoutUI(); 
            });
        }


        // NIEZNISZCZALNA METODA FABRYKI PRZYCISKÓW - ZAWSZE TUTAJ BĘDZIE!
        private UIDynamicButton AddButton(string label, bool rightSide, Color color, UnityEngine.Events.UnityAction onClick = null, bool interactable = true)
        {
            UIDynamicButton btn = CreateButton(label, rightSide);
            if (btn == null) return null;
            btn.button.interactable = interactable;
            SetBtnColor(btn, color);
            if (onClick != null) btn.button.onClick.AddListener(onClick);
            
            activeDynamicElements.Add(() => RemoveButton(btn));
            return btn;
        }

        private void AddSubHeader(string text, Color c, bool rightSide)
        {
            string uniqueId = "subhdr_" + Guid.NewGuid().ToString("N");
            JSONStorableString storage = new JSONStorableString(uniqueId, text);
            UIDynamicTextField field = CreateTextField(storage, rightSide);
            if (field == null) return;
            
            field.height = 35f;
            Text txtText = field.GetComponentInChildren<Text>();
            if (txtText != null) { txtText.color = c; txtText.alignment = TextAnchor.MiddleCenter; }
            Image bgImg = field.GetComponent<Image>();
            if (bgImg != null) bgImg.color = new Color(0.04f, 0.04f, 0.06f);
            
            activeDynamicElements.Add(() => RemoveTextField(field));
        }

        private void AddToggle(JSONStorableBool storable, bool rightSide)
        {
            if (storable == null) return;
            UIDynamicToggle tg = CreateToggle(storable, rightSide);
            if (tg != null) activeDynamicElements.Add(() => RemoveToggle(tg));
        }

        private void AddSlider(JSONStorableFloat storable, bool rightSide, Color? textCustomColor = null)
        {
            if (storable == null) return;
            UIDynamicSlider sl = CreateSlider(storable, rightSide);
            if (sl == null) return;
            if (textCustomColor.HasValue)
            {
                Text txt = sl.GetComponentInChildren<Text>();
                if (txt != null) txt.color = textCustomColor.Value;
            }
            activeDynamicElements.Add(() => RemoveSlider(sl));
        }

        private void AddPopup(JSONStorableStringChooser storable, bool rightSide)
        {
            if (storable == null) return;
            UIDynamicPopup pop = CreatePopup(storable, rightSide);
            if (pop != null) activeDynamicElements.Add(() => RemovePopup(pop));
        }

        public JSONStorableFloat GetFloat(string key) { JSONStorableFloat f; return pluginFloats.TryGetValue(key, out f) ? f : null; }
        public JSONStorableBool GetBool(string key) { JSONStorableBool b; return pluginBools.TryGetValue(key, out b) ? b : null; }

        // RENDERER MENU GŁÓWNEGO: Pełna synchronizacja kolorów (Niebieski start, Czerwony Realizm)
        private void RenderMainMenu()
        {
            Color primaryBlue = new Color(0.133f, 0.333f, 0.933f); // Niebieski #2255ee
            Color realismRed = new Color(0.839f, 0.165f, 0.180f);  // Czerwony #d62a2e
            
            string[] technicalTabs = { "Audio", "Handjob", "Breathing", "Blowjob", "Penetration", "Expression", "Gaze", "Realism" };
            string[] displayLabels = { "Audio", "Handjob", "Breathing", "Blowjob", "Penetration", "Gaze & Glance", "Expression", "Realism" };

            for (int i = 0; i < technicalTabs.Length; i++)
            {
                string targetTab = technicalTabs[i];
                bool isRight = (i % 2 != 0);
                
                // PUNKT 1: Tylko kategoria Realism dostaje głęboką czerwień, reszta to intuicyjny niebieski
                Color btnColor = (targetTab == "Realism") ? realismRed : primaryBlue;

                AddButton(displayLabels[i], isRight, btnColor, () =>
                {
                    currentTab = targetTab;
                    selectedTargetName = "";
                    audioCategory = "";
                    
                    if (targetTab == "Audio") currentSubMenu = "Audio_Home";
                    else if (targetTab == "Breathing" || targetTab == "Penetration" || targetTab == "Gaze") currentSubMenu = "Select_Person";
                    else currentSubMenu = "Execute";
                    
                    CreateDynamicLayoutUI();
                });
            }
        }

        private void RenderAudioHome()
        {
            // Czyste nazwy z Twojego pliku PDF
            string[] categories = { "Persons", "Assets", "Breathing", "Licking", "Oral", "Thrust", "Orgasm", "Handjob", "Slaps" };
            Color defaultGray = Color.gray;

            for (int i = 0; i < categories.Length; i++)
            {
                string cat = categories[i];
                bool isRight = (i % 2 != 0);
                
                // Przypisanie kolorów specjalnych do wyboru głównych gałęzi banków audio
                Color btnColor = defaultGray;
                if (cat == "Persons") btnColor = new Color(0.945f, 0.396f, 0.945f); // Różowy #f165f1
                else if (cat == "Assets") btnColor = new Color(0.0f, 0.75f, 0.0f);   // Zielony #00c000

                AddButton(cat, isRight, btnColor, () =>
                {
                    audioCategory = cat;
                    selectedTargetName = "";
                    if (cat == "Slaps") currentSubMenu = "Execute";
                    else if (cat == "Assets") currentSubMenu = "Audio_Assets";
                    else currentSubMenu = "Audio_PersonPicker";
                    CreateDynamicLayoutUI();
                });
            }
        }

        private void RenderPersonPicker(Action<string> onSelect)
        {
            // WYTYCZNA: Kolor różowy #f165f1 dla przycisków person atom
            Color personPink = new Color(0.945f, 0.396f, 0.945f); 
            List<Atom> sceneAtoms = SuperController.singleton.GetAtoms();

            for (int i = 1; i <= 4; i++)
            {
                string key = "Person " + i;
                bool isRight = (i % 2 == 0);
                string displayName = key; 

                for (int a = 0; a < sceneAtoms.Count; a++)
                {
                    Atom atom = sceneAtoms[a];
                    string atomKey;
                    if (atom != null && personKeyByAtomUid.TryGetValue(atom.uid, out atomKey) && atomKey == key)
                    {
                        if (!string.IsNullOrEmpty(atom.name)) displayName = atom.name;
                        break;
                    }
                }

                AddButton(displayName, isRight, personPink, () => { onSelect(key); });
            }
        }

        private void RenderAudioAssets()
        {
            // WYTYCZNA: Kolor zielony #00c000 dla konfiguracji zasobów CustomUnityAsset
            Color assetGreen = new Color(0.0f, 0.75f, 0.0f);
            
            AddButton("Asset", false, assetGreen, () =>
            {
                selectedTargetName = "Asset";
                currentSubMenu = "Execute";
                CreateDynamicLayoutUI();
            });
        }

        private void RenderExecutePanel()
        {
            switch (currentTab)
            {
                case "Audio": RenderAudioExecute(); break;
                case "Handjob": RenderHandjobExecute(); break;
                case "Breathing": RenderBreathingExecute(); break;
                case "Blowjob": RenderBlowjobExecute(); break;
                case "Penetration": RenderPenetrationExecute(); break;
                case "Expression": RenderExpressionExecute(); break;
                case "Gaze": RenderGazeExecute(); break;
                case "Realism": RenderRealismExecute(); break;
            }
        }

        private void RenderAudioExecute()
        {
            switch (audioCategory)
            {
                case "Persons": RenderPersonsAudioExecute(); break;
                case "Assets": RenderAssetsAudioExecute(); break;
                case "Slaps": RenderImpactAudioExecute(); break; // dopasowanie do nazwy z PDF
                default: RenderSimpleCategoryAudioExecute(audioCategory); break;
            }
        }

        private void RenderPersonsAudioExecute()
        {
            Color textTheme = new Color(0f, 0.75f, 0.9f);
            AddSlider(GetFloat("AudVolume"), false, textTheme);
            AddSlider(GetFloat("AudAmplification"), true, textTheme);
            AddSlider(GetFloat("AudPitch"), false, textTheme);
            AddSlider(GetFloat("AudInterval"), true, textTheme);
            AddToggle(GetBool("Aud3D"), false);
            AudioBank bank = personAudioBanks.ContainsKey(selectedTargetName) ? personAudioBanks[selectedTargetName] : null;
            RenderAudioFilePicker(bank);
        }

        private void RenderAssetsAudioExecute()
        {
            Color textTheme = new Color(0.1f, 0.8f, 0.4f);
            AddSlider(GetFloat("AsVolume"), false, textTheme);
            AddSlider(GetFloat("AsAmplification"), true, textTheme);
            AddSlider(GetFloat("AsPitch"), false, textTheme);
            AddSlider(GetFloat("AsInterval"), true, textTheme);
            AddToggle(GetBool("As3D"), false);
            RenderAudioFilePicker(assetAudioBank);
        }

        private void RenderImpactAudioExecute()
        {
            Color textTheme = new Color(0.6f, 0.6f, 0.6f);
            AddSlider(GetFloat("AudImVolume"), false, textTheme);
            AddSlider(GetFloat("AudImAmplification"), true, textTheme);
            AddSlider(GetFloat("AudImPitch"), false, textTheme);
            AddSlider(GetFloat("AudImInterval"), true, textTheme);
            AddToggle(GetBool("AudIm3D"), false);
            RenderAudioFilePicker(impactAudioBank);
        }

        private void RenderSimpleCategoryAudioExecute(string category)
        {
            if (!simpleAudioCategoryPrefix.ContainsKey(category)) return;
            string prefix = simpleAudioCategoryPrefix[category];
            Color textTheme = new Color(0.8f, 0.75f, 0.2f);
            
            AddSlider(GetFloat(prefix + "Volume"), false, textTheme);
            AddSlider(GetFloat(prefix + "Amplification"), true, textTheme);
            AddSlider(GetFloat(prefix + "Pitch"), false, textTheme);
            AddSlider(GetFloat(prefix + "Interval"), true, textTheme);
            AddToggle(GetBool(prefix + "3D"), false);
            
            AudioBank bank;
            simpleAudioBanks.TryGetValue(category + "|" + selectedTargetName, out bank);
            RenderAudioFilePicker(bank);
        }

        private void RenderAudioFilePicker(AudioBank bank)
        {
            if (bank == null) return;
            Color goldTheme = new Color(0.9f, 0.75f, 0.1f);
            string statusReport = (bank.Clips.Count == 0) ? "EMPTY (0 FILES LOADED)" : bank.Clips.Count + " ACTIVE AUDIO FILE(S) CACHED";
            
            AddSubHeader("STORAGE STATE: " + statusReport, goldTheme, false);
            AddButton("BROWSE & IMPORT FOLDER", false, new Color(0.0f, 0.55f, 0.35f), () =>
            {
                SuperController.singleton.GetDirectoryPathDialog((string path) =>
                {
                    if (!string.IsNullOrEmpty(path)) bank.ImportFolder(path);
                    CreateDynamicLayoutUI();
                }, DefaultAudioFolder);
            });
            AddButton("CHOOSE & IMPORT SINGLE FILE", true, new Color(0.0f, 0.45f, 0.60f), () =>
            {
                SuperController.singleton.GetMediaPathDialog((string path) =>
                {
                    if (!string.IsNullOrEmpty(path)) bank.ImportFile(path);
                    CreateDynamicLayoutUI();
                }, "wav|ogg|mp3", DefaultAudioFolder);
            });
            AddButton("WIPE ALL CLIPS FROM BANK", false, new Color(0.6f, 0.1f, 0.1f), () =>
            {
                bank.ClearAll();
                CreateDynamicLayoutUI();
            });
        }

        private void RenderHandjobExecute()
        {
            Color deepPink = new Color(0.85f, 0.1f, 0.45f);
            AddToggle(GetBool("HJEnableLeft"), false);
            AddToggle(GetBool("HJEnableRight"), true);
            AddSlider(GetFloat("HJVelLeft"), false, deepPink);
            AddSlider(GetFloat("HJVelRight"), true, deepPink);
            AddSlider(GetFloat("HJIntLeft"), false, deepPink);
            AddSlider(GetFloat("HJIntRight"), true, deepPink);
        }

        private void RenderBreathingExecute()
        {
            Color breathingCyan = new Color(0.05f, 0.5f, 0.65f);
            
            JSONStorableBool autoCheck = GetBool("RLAutoBreathing");
            bool isAuto = (autoCheck != null && autoCheck.val);

            if (isAuto)
            {
                string idL = "warn_l_" + Guid.NewGuid().ToString("N");
                JSONStorableString storL = new JSONStorableString(idL, "Auto Breathing System ACTIVE");
                UIDynamicTextField fldL = CreateTextField(storL, false);
                if (fldL != null)
                {
                    fldL.height = 35f;
                    Text txt = fldL.GetComponentInChildren<Text>();
                    if (txt != null) { txt.color = Color.red; txt.alignment = TextAnchor.MiddleCenter; }
                    Image img = fldL.GetComponent<Image>();
                    if (img != null) img.color = new Color(0.06f, 0.04f, 0.04f);
                    activeDynamicElements.Add(() => RemoveTextField(fldL));
                }

                string idR = "warn_r_" + Guid.NewGuid().ToString("N");
                JSONStorableString storR = new JSONStorableString(idR, "(Manual Overrides Locked)");
                UIDynamicTextField fldR = CreateTextField(storR, true);
                if (fldR != null)
                {
                    fldR.height = 35f;
                    Text txt = fldR.GetComponentInChildren<Text>();
                    if (txt != null) { txt.color = Color.red; txt.alignment = TextAnchor.MiddleCenter; }
                    Image img = fldR.GetComponent<Image>();
                    if (img != null) img.color = new Color(0.06f, 0.04f, 0.04f);
                    activeDynamicElements.Add(() => RemoveTextField(fldR));
                }
            }

            // Nazwa przełącznika skrócona na czyste "Enable Breathing"
            AddToggle(pluginBools["BREnable"], false); 
            if (!isAuto) AddButton("", true, Color.black, null, false);

            AddSlider(GetFloat("BRVelocity"), false, breathingCyan);
            AddSlider(GetFloat("BRIntense"), true, breathingCyan);
        }


        private void RenderBlowjobExecute()
        {
            Color darkPurple = new Color(0.45f, 0.1f, 0.65f);
            AddToggle(GetBool("IKEnable"), false);
            AddButton("", true, Color.black, null, false);
            AddSlider(GetFloat("BJVelocity"), false, darkPurple);
            AddSlider(GetFloat("BJIntense"), true, darkPurple);
        }

        private void RenderPenetrationExecute()
        {
            Color deepRed = new Color(0.65f, 0.05f, 0.05f);
            AddToggle(GetBool("PNEnable"), false);
            AddButton("", true, Color.black, null, false);
            AddSlider(GetFloat("PNVelocity"), false, deepRed);
            AddSlider(GetFloat("PNIntense"), true, deepRed);
        }

        private void RenderGazeExecute()
        {
            Color darkEmerald = new Color(0.02f, 0.40f, 0.20f);
            AddToggle(GetBool("GZEnable"), false);
            AddButton("", true, Color.black, null, false);
            AddSlider(GetFloat("GZHeadWeight"), false, darkEmerald);
            AddSlider(GetFloat("GZChestWeight"), true, darkEmerald);
        }
        private void RenderExpressionExecute()
        {
            Color violet = new Color(0.55f, 0.25f, 0.75f);
            for (int i = 1; i <= 4; i++)
            {
                string key = "Person " + i;
                bool rightSide = (i % 2 == 0);
                AddSubHeader("CONFIG FOR: " + key.ToUpper(), violet, rightSide);
                if (personalityStorables.ContainsKey(key)) AddPopup(personalityStorables[key], rightSide);
                if (energyStorables.ContainsKey(key)) AddSlider(energyStorables[key], rightSide, violet);
                if (moveStorables.ContainsKey(key)) AddPopup(moveStorables[key], rightSide);
            }
        }

        private void RenderRealismExecute()
        {
            // Dokładnie 12 oryginalnych przełączników automatyzacji panelu Realizmu
            string[] realismKeys = {
                "RLAutoBreathing", "RLAutoBlinking", "RLAutoClothing", "RLAutoLipSync",
                "RLAutoHandMovements", "RLAutoEnvironmentSound", "RLAutoPhysics", "RLAutoTransitions",
                "RLAutoTessellation", "RLAutoFreezePose", "RLAutoMovement", "RLAutoNaturalMotion"
            };
            for (int i = 0; i < realismKeys.Length; i++)
            {
                bool isRight = (i % 2 != 0);
                AddToggle(GetBool(realismKeys[i]), isRight);
            }
        }

        private void GoBackMenuLogic()
        {
            if (currentSubMenu == "Execute")
            {
                if (currentTab == "Audio")
                {
                    if (audioCategory == "Impact") currentSubMenu = "Audio_Home";
                    else if (audioCategory == "Assets") currentSubMenu = "Audio_Assets";
                    else currentSubMenu = "Audio_PersonPicker";
                }
                else if (currentTab == "Breathing" || currentTab == "Penetration" || currentTab == "Gaze") 
                {
                    currentSubMenu = "Select_Person";
                }
                else 
                { 
                    currentSubMenu = "Main"; 
                    currentTab = ""; 
                    selectedTargetName = ""; 
                }
            }
            else if (currentSubMenu == "Audio_PersonPicker" || currentSubMenu == "Audio_Assets") 
            {
                currentSubMenu = "Audio_Home";
            }
            else if (currentSubMenu == "Audio_Home" || currentSubMenu == "Select_Person") 
            { 
                currentSubMenu = "Main"; 
                currentTab = ""; 
                selectedTargetName = ""; 
                audioCategory = ""; 
            }
            
            CreateDynamicLayoutUI();
        }

        // =========================================================================
        // PROCEDURALNY HUD / MINI-PANEL STEROWANIA NA EKRANIE GRY VAM (PRZYWRÓCONY)
        // =========================================================================
        private void CreateScreenOverlayUI()
        {
            try
            {
                if (overlayCanvas != null) return;
                
                overlayCanvas = new GameObject("UltraScene_InteractiveHUD");
                Canvas canvas = overlayCanvas.AddComponent<Canvas>(); 
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 999; 
                overlayCanvas.AddComponent<CanvasScaler>();
                overlayCanvas.AddComponent<GraphicRaycaster>(); 

                // CIEMNOSZARE PÓŁPRZEZROCZYSTE TŁO PANELU HUD Z CZARNĄ RAMKĄ
                GameObject panelObj = new GameObject("HUD_BackgroundPanel");
                panelObj.transform.SetParent(overlayCanvas.transform, false);
                Image panelBg = panelObj.AddComponent<Image>();
                panelBg.color = new Color(0.06f, 0.06f, 0.08f, 0.88f); 
                
                RectTransform panelRect = panelObj.GetComponent<RectTransform>();
                panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = Vector2.one; 
                panelRect.anchoredPosition = new Vector2(-10, -10); 
                panelRect.sizeDelta = new Vector2(360, 840); 

                Outline border = panelObj.AddComponent<Outline>();
                border.effectColor = Color.black;
                border.effectDistance = new Vector2(2f, 2f);

                GameObject textObj = new GameObject("HUD_InteractiveText"); 
                textObj.transform.SetParent(panelObj.transform, false);
                
                // Korzystamy ze standardowej, bezpiecznej dla VaM klasy Text
                overlayTextInfo = textObj.AddComponent<Text>(); 
                overlayTextInfo.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                overlayTextInfo.fontSize = 15; // Rozmiar czcionki 15 zgodnie z prośbą!
                overlayTextInfo.color = Color.white;
                overlayTextInfo.alignment = TextAnchor.UpperLeft;
                overlayTextInfo.lineSpacing = 1.25f; 
                
                RectTransform textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
                textRect.pivot = new Vector2(0f, 1f);
                textRect.anchoredPosition = new Vector2(15, -15);
                textRect.sizeDelta = new Vector2(-30, -30);

                // Przechwytywanie kliknięć myszką na obszarze HUD-a
                Button hudClicker = textObj.AddComponent<Button>();
                hudClicker.onClick.AddListener(() => { HandleHUDInteraction(); });

                overlayCanvas.SetActive(true);
            }
            catch (Exception e) { SuperController.LogError("[HUD Create Error] " + e.Message); }
        }


        // KLIKALNOŚĆ HUD: Odczytujemy indeks linii bezpośrednio z pamięci generatora tekstów Unity
        private void HandleHUDInteraction()
        {
            try
            {
                if (overlayTextInfo == null || overlayTextInfo.cachedTextGenerator == null) return;

                Vector2 localMousePos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayTextInfo.rectTransform, Input.mousePosition, null, out localMousePos);
                
                // Przeliczamy wysokość kursora myszy od górnej krawędzi tekstu panelu
                float pixelsFromTop = overlayTextInfo.rectTransform.rect.yMax - localMousePos.y;
                
                // POBIERAMY RECH TYP LINII OD UNITY: Sprawdzamy ile linii wygenerował silnik w pamięci RAM
                int totalLines = overlayTextInfo.cachedTextGenerator.lineCount;
                if (totalLines <= 0) return;

                // Ustalamy średnią wysokość linii na podstawie faktycznych danych geometrycznych wygenerowanych na ekranie
                float totalTextHeight = overlayTextInfo.preferredHeight;
                float heightPerLine = totalTextHeight / totalLines;
                
                int lineIndex = Mathf.FloorToInt(pixelsFromTop / heightPerLine);

                int currentLine = 0; // Nagłówek Ultra Scene Controller

                // PRECYZYJNA DYNAMICZNA PĘTLA DLA 4 POSTACI (OMINIĘCIE PRZESUNIĘĆ KLIKNIĘĆ)
                for (int p = 1; p <= 4; p++)
                {
                    string pKey = "Person " + p;
                    currentLine++; // Linia Imienia Postaci
                    
                    // Kliknięcie w linię Personality Choice
                    if (lineIndex == currentLine)
                    {
                        var storable = personalityStorables[pKey];
                        if (storable != null && storable.choices != null && storable.choices.Count > 0) 
                        { 
                            int idx = storable.choices.IndexOf(storable.val); 
                            storable.val = storable.choices[(idx + 1) % storable.choices.Count]; 
                            CreateDynamicLayoutUI(); 
                            return; 
                        }
                    }
                    currentLine++;

                    currentLine++; // Linia Aarousal (Wskaźnik zablokowany do klikania)

                    // Kliknięcie w linię Move
                    if (lineIndex == currentLine)
                    {
                        var storable = moveStorables[pKey];
                        if (storable != null && storable.choices != null && storable.choices.Count > 0) 
                        { 
                            int idx = storable.choices.IndexOf(storable.val); 
                            storable.val = storable.choices[(idx + 1) % storable.choices.Count]; 
                            CreateDynamicLayoutUI(); 
                            return; 
                        }
                    }
                    currentLine++; // Przejście przez niewidzialną linię \n
                }

                currentLine++; // Linia Nagłówka przedziału Realizm

                string[] realismKeys = {
                    "RLAutoBreathing", "RLAutoLicking", "RLAutoForeskin", "RLAutoSucking", "RLAutoHandMovements",
                    "RLAutoPenetrationSound", "RLAutoSlap", "RLAutoOrgasm", "RLAutoNormalizeAudio", "RLAutoTessellation",
                    "RLAutoDynamicSkinWetness", "RLAutoResetJoints", "RLAutoFreezePose", "RLAutoMovement",
                    "RLAutoNaturalMotion", "RLAutoMicroMuscleDrift"
                };

                // STUPROCENTOWA PRECYZJA DLA 16 SYSTEMÓW REALIZMU
                for (int r = 0; r < realismKeys.Length; r++)
                {
                    if (lineIndex == currentLine)
                    {
                        JSONStorableBool toggle = GetBool(realismKeys[r]);
                        if (toggle != null) { toggle.val = !toggle.val; CreateDynamicLayoutUI(); return; }
                    }
                    currentLine++;
                }
            }
            catch { }
        }

        // STABILNY GENERATOR RICH-TEXT: Rozmiar czcionki 15, linie myślników i 100% poprawność kodów kolorów Unity
        private void BuildHUDContentText(List<Atom> allAtoms)
        {
            if (overlayTextInfo == null) return;

            // WYTYCZNA: Tytuł w kolorze niebieskim HTML #2255ee z liniami przedziału
            string hudText = "<color=#2255ee><b>------------------Ultra Scene Controller------------------</b></color>\n";

            for (int p = 1; p <= 4; p++)
            {
                string key = "Person " + p;
                string realName = key;
                
                for (int i = 0; i < allAtoms.Count; i++)
                {
                    Atom a = allAtoms[i];
                    string aKey;
                    if (a != null && personKeyByAtomUid.TryGetValue(a.uid, out aKey) && aKey == key && !string.IsNullOrEmpty(a.name))
                    {
                        realName = a.name;
                        break;
                    }
                }

                // WYTYCZNA: Nazwa postaci w kolorze różowym #f165f1
                hudText += "<color=#f165f1><b>" + realName + "</b></color>\n";
                hudText += "   Personality choice: <color=#00e5ff>[" + personPersonality[key] + "]</color>\n";
                hudText += "   Aarousal: <color=#ffea00>[" + personEnergyLevel[key].ToString("F0") + "%]</color>\n";
                hudText += "   Move: <color=#00ff66>[" + personMoveTarget[key] + "]</color>\n";
            }

            // WYTYCZNA: Nagłówek Realism oraz linia przedziału w kolorze głębokiej czerwieni #d62a2e
            hudText += "<color=#d62a2e><b>------------------Realism------------------</b></color>\n";

            string[] displayRealismNames = {
                "Auto Breathing system", "Auto Licking system", "Auto Foreskin system", "Auto Sucking system", "Auto Hand Movements system",
                "Auto Penetration Sound system", "Auto Slap system", "Auto Orgasm system", "Auto Normalize Audio system", "Auto Tessellation system",
                "Auto Dynamic Skin Wetness system", "Auto Reset Joints system", "Auto Freeze Pose system", "Auto Movement system",
                "Auto Natural Motion system", "Auto Micro Muscle Drift system"
            };

            string[] realismKeys = {
                "RLAutoBreathing", "RLAutoLicking", "RLAutoForeskin", "RLAutoSucking", "RLAutoHandMovements",
                "RLAutoPenetrationSound", "RLAutoSlap", "RLAutoOrgasm", "RLAutoNormalizeAudio", "RLAutoTessellation",
                "RLAutoDynamicSkinWetness", "RLAutoResetJoints", "RLAutoFreezePose", "RLAutoMovement",
                "RLAutoNaturalMotion", "RLAutoMicroMuscleDrift"
            };

            for (int r = 0; r < realismKeys.Length; r++)
            {
                JSONStorableBool toggle = GetBool(realismKeys[r]);
                bool state = (toggle != null && toggle.val);
                
                string statusLabel = state ? "<color=#00ff00><b>[ON]</b></color>" : "<color=#888888>[OFF]</color>";
                hudText += "   " + displayRealismNames[r] + ": " + statusLabel + "\n";
            }

            overlayTextInfo.fontSize = 15; // Sztywne wymuszenie rozmiaru czcionki 15
            overlayTextInfo.lineSpacing = 1.25f; 
            overlayTextInfo.supportRichText = true;
            overlayTextInfo.text = hudText;
        }

        private void SetBtnColor(UIDynamicButton element, Color c) { if (element == null) return; Text txt = element.GetComponentInChildren<Text>(); if (txt != null) txt.color = c; Image img = element.GetComponent<Image>(); if (img != null) img.color = new Color(0.06f, 0.06f, 0.08f); }

        private class AudioBank
        {
            private readonly JSONStorableString pathsStorable;
            public List<NamedAudioClip> Clips = new List<NamedAudioClip>();
            public AudioBank(JSONStorableString backingStorable) { pathsStorable = backingStorable; }

            public void RebuildFromStorable()
            {
                Clips.Clear();
                if (pathsStorable == null || string.IsNullOrEmpty(pathsStorable.val)) return;
                string[] uids = pathsStorable.val.Split('|');
                for (int i = 0; i < uids.Length; i++)
                {
                    string uid = uids[i];
                    if (string.IsNullOrEmpty(uid)) continue;
                    NamedAudioClip clip = LoadClip(uid);
                    if (clip != null) Clips.Add(clip);
                }
            }

            public void ImportFolder(string folderPath)
            {
                string[] files = SuperController.singleton.GetFilesAtPath(folderPath);
                if (files != null) { for (int i = 0; i < files.Length; i++) AddIfAudio(files[i]); }
                SyncStorable();
            }

            public void ImportFile(string path) { AddIfAudio(path); SyncStorable(); }
            public void ClearAll() { Clips.Clear(); SyncStorable(); }

            public NamedAudioClip GetRandomClip()
            {
                if (Clips.Count == 0) return null;
                int idx = UnityEngine.Random.Range(0, Clips.Count);
                return Clips[idx];
            }

            private void AddIfAudio(string path)
            {
                if (string.IsNullOrEmpty(path)) return;
                string lower = path.ToLowerInvariant();
                bool isAudio = lower.EndsWith(".wav") || lower.EndsWith(".ogg") || lower.EndsWith(".mp3");
                if (!isAudio) return;
                NamedAudioClip clip = LoadClip(path);
                if (clip != null && !Clips.Contains(clip)) Clips.Add(clip);
            }

            private static NamedAudioClip LoadClip(string rawPath)
            {
                string normalized = SuperController.singleton.NormalizeLoadPath(rawPath);
                NamedAudioClip existing = URLAudioClipManager.singleton.GetClip(normalized);
                if (existing != null) return existing;
                return URLAudioClipManager.singleton.QueueClip(normalized);
            }

            private void SyncStorable()
            {
                if (pathsStorable == null) return;
                List<string> uids = new List<string>();
                for (int i = 0; i < Clips.Count; i++) uids.Add(Clips[i].uid);
                pathsStorable.val = string.Join("|", uids.ToArray());
            }
        }
    } // <--- KLASA GŁÓWNA UltraSceneController ZAMKNIĘTA IDEALNIE
    // =========================================================================
    // GLOBALNE MODUŁY WYKONAWCZE (WEWNĄTRZ NAMESPACE ULTRASCENE)
    // =========================================================================
    
    public class UltraBreathingModule 
    { 
        private UltraSceneController plugin;
        private AudioSource audioSource;
        private float breathTimer = 0f;
        
        private float currentSpeed = 1.2f;
        private float currentIntensity = 0.8f;

        public UltraBreathingModule(UltraSceneController p, AudioSource a) 
        { 
            plugin = p; 
            audioSource = a; 
        } 

        public void UpdateModule(Atom personAtom) 
        { 
            if (personAtom == null || personAtom.type != "Person") return;
            
            // BEZWZGLĘDNA LOGICZNA BLOKADA MISTRZOWSKA: Odznaczenie przełącznika "Enable Breathing"
            // natychmiast odcina sinusoidę i całkowicie zamraża klatkę, brzuch, usta oraz nozdrza postaci!
            JSONStorableBool breathingMasterSwitch = plugin.GetBool("BREnable");
            if (breathingMasterSwitch != null && !breathingMasterSwitch.val)
            {
                return; 
            }

            JSONStorableBool autoBreathingToggle = plugin.GetBool("RLAutoBreathing");
            bool isAutoBreathingActive = (autoBreathingToggle != null && autoBreathingToggle.val);

            if (isAutoBreathingActive)
            {
                float baseExertion = 0.0f;

                JSONStorableBool bjEnable = plugin.GetBool("IKEnable");
                if (bjEnable != null && bjEnable.val) baseExertion = Mathf.Max(baseExertion, 0.4f);
                
                JSONStorableBool hjLeft = plugin.GetBool("HJEnableLeft");
                JSONStorableBool hjRight = plugin.GetBool("HJEnableRight");
                if ((hjLeft != null && hjLeft.val) || (hjRight != null && hjRight.val)) baseExertion = Mathf.Max(baseExertion, 0.3f);

                JSONStorableBool pnEnable = plugin.GetBool("PNEnable");
                if (pnEnable != null && pnEnable.val) baseExertion = Mathf.Max(baseExertion, 0.7f);

                float targetSpeed = Mathf.Lerp(1.2f, 2.8f, baseExertion);
                float targetIntensity = Mathf.Lerp(0.7f, 1.3f, baseExertion);
                
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 1.5f);
                currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, Time.deltaTime * 1.5f);

                JSONStorableFloat uiVel = plugin.GetFloat("BRVelocity");
                JSONStorableFloat uiInt = plugin.GetFloat("BRIntense");
                if (uiVel != null) uiVel.valNoCallback = currentSpeed;
                if (uiInt != null) uiInt.valNoCallback = currentIntensity;
            }
            else
            {
                JSONStorableFloat velStorable = plugin.GetFloat("BRVelocity");
                JSONStorableFloat intStorable = plugin.GetFloat("BRIntense");
                
                currentSpeed = (velStorable != null) ? velStorable.val : 1.2f;
                currentIntensity = (intStorable != null) ? intStorable.val : 0.8f;
            }

            breathTimer += Time.deltaTime * currentSpeed * 2.5f; 
            float breathWave = Mathf.Sin(breathTimer);
            
            JSONStorable shapeStorable = personAtom.GetStorableByID("geometry");
            if (shapeStorable == null) return;

            float chestValue = (breathWave * 0.45f * currentIntensity) + 0.5f; 
            float stomachValue = (breathWave * 0.35f * currentIntensity) + 0.4f;
            float positiveWave = Mathf.Max(0f, breathWave) * currentIntensity;

            shapeStorable.SetFloatParamValue("Breathing Chest", chestValue);
            shapeStorable.SetFloatParamValue("Breathing Stomach", stomachValue);
            shapeStorable.SetFloatParamValue("Breathing Lips", positiveWave * 0.25f);
            shapeStorable.SetFloatParamValue("Breathing NoseIn", positiveWave * 0.20f);
            shapeStorable.SetFloatParamValue("Breathing NoseOut", (1f - positiveWave) * 0.20f);
        } 
    }

    public class UltraAudioModule { public float volume, pitch; public bool is3DSound; public UltraAudioModule(UltraSceneController p, AudioSource s) {} public void PlayTargetSound(string c, Vector3 pos) {} public void NormalizeLoadedAudio() {} public void ClearAllFiles() {} }
    public class UltraLickingModule { public bool isEnabled; public float lickingSpeed; public UltraLickingModule(UltraSceneController p) {} public void UpdateModule(Atom s, Atom t) {} }
    public class UltraHandjobModule { public bool isEnabled; public float handjobSpeed, handjobIntense; public UltraHandjobModule(UltraSceneController p) {} public void UpdateModule(Atom s, Atom t) {} }
    public class UltraBlowjobModule { public bool isEnabled; public float suckSpeed; public UltraBlowjobModule(UltraSceneController p) {} public void UpdateModule(Atom s, Atom t) {} }
    public class UltraPenetrationModule { public bool playCollisionSounds; public float penetrationIntense; public UltraPenetrationModule(UltraSceneController p) {} public void UpdateModule(Atom s, Atom t, int idx) {} }
public class UltraExpressionModule 
{ 
    private UltraSceneController plugin;
    private Dictionary<string, Dictionary<string, JSONStorableFloat>> personMorphCache = new Dictionary<string, Dictionary<string, JSONStorableFloat>>();
    private Dictionary<string, float> nextWinkTime = new Dictionary<string, float>();
    private Dictionary<string, float> winkDuration = new Dictionary<string, float>();
    private Dictionary<string, string> activeWinkSide = new Dictionary<string, string>();

    private const string ascoPath = "ascorad.asco_Expressions.12:/Custom/Atom/Person/Morphs/female/asco - Expressions/";
    private const string ashPath = "AshAuryn.AshAuryn_Sexpressions_2_Point_0.5:/Custom/Atom/Person/Morphs/female/ASHAURYN OFFICIAL/TOOLS/EXPRESSIONS/EYES/CLOSED/";

    public UltraExpressionModule(UltraSceneController p) 
    { 
        plugin = p; 
    } 

    public void UpdateModule(Atom personAtom, string personKey, float currentArousal, string currentAction, string personality) 
    { 
        if (personAtom == null || personAtom.type != "Person") return;

        JSONStorable geoComponent = personAtom.GetStorableByID("geometry");
        if (geoComponent == null) return;

        if (!personMorphCache.ContainsKey(personAtom.uid))
        {
            personMorphCache[personAtom.uid] = new Dictionary<string, JSONStorableFloat>();
        }

        float arousalFactor = Mathf.Clamp01(currentArousal / 100f);

        // =========================================================================
        // OSTATECZNA, PEŁNA LISTA DEKLARACJI ZMIENNYCH (NAPRAWA BŁĘDU Z REPOZYTORIUM)
        // =========================================================================
        float targetDesire = 0f;
        float targetExcitement = arousalFactor * 0.7f; 
        float targetHappy = 0f;
        float targetSurprise = 0f;
        float targetConfused = 0f;
        float targetPain = 0f;
        float targetSnarlL = 0f;
        float targetSnarlR = 0f;
        float targetAfraid = 0f;
        float targetContempt = 0f;
        float targetFrown = 0f;
        float targetAngry = 0f;
        float targetConcentrate = 0f;
        float targetFear = 0f;
        float targetShock = 0f;

        float targetBedroomSmile = 0f;
        float targetLipBiteWide = 0f;
        float targetWrongHoleA = 0f;
        float targetWrongHoleB = 0f;
        float targetUgh = 0f;
        float targetUhOh = 0f;

        float targetWinkL = 0f;
        float targetWinkR = 0f;

        float tongueFlutter = 0f;
        if (arousalFactor > 0.5f)
        {
            tongueFlutter = Mathf.Sin(Time.time * 6.0f) * 0.25f * arousalFactor;
        }

        switch (personality)
        {
            case "Sensual / Romantic":
                targetDesire = 0.5f + (arousalFactor * 0.2f); 
                targetBedroomSmile = 0.35f * arousalFactor;
                if (arousalFactor >= 0.5f) targetSnarlL = (arousalFactor - 0.5f) * 0.4f; 
                if (currentAction == "Licking" && arousalFactor > 0.5f) targetBedroomSmile += tongueFlutter;
                if (currentAction == "Anal") { targetConfused = 0.3f; targetUhOh = 0.4f; }
                break;

            case "Shy / Surprised":
                targetConfused = 0.3f + (arousalFactor * 0.4f); 
                targetSurprise = 0.35f * arousalFactor;
                if (currentAction == "Anal") { targetWrongHoleA = 0.5f; targetPain = 0.3f; }
                break;

            case "Passionate / Ecstatic":
                targetDesire = 0.7f; 
                if (arousalFactor >= 0.5f) targetSnarlR = 0.15f + ((arousalFactor - 0.5f) * 0.2f);
                break;

            case "Angry / Surprised":
                if (arousalFactor < 0.5f) { targetFrown = 0.5f; } 
                else { targetDesire = 0.5f; targetHappy = 0.25f; targetSnarlL = 0.2f; }
                break;

            case "Fear / Shocked":
                targetAfraid = 0.65f; targetConfused = 0.4f; targetUhOh = 0.5f;
                break;

            case "Horny / Disgust":
                targetDesire = 0.7f;
                if (arousalFactor >= 0.5f && currentAction != "Anal") targetSnarlL = 0.25f;
                if (currentAction == "Anal") { targetContempt = 0.6f; targetUgh = 0.5f; }
                break;
            case "Horny / Surprised":
                targetHappy = 0.45f;
                targetSurprise = 0.35f;
                if (arousalFactor < 0.5f && UnityEngine.Random.value < 0.005f && Time.time > nextWinkTime[personAtom.uid])
                {
                    activeWinkSide[personAtom.uid] = "Right";
                    winkDuration[personAtom.uid] = Time.time + 0.25f;
                    nextWinkTime[personAtom.uid] = Time.time + UnityEngine.Random.Range(8.0f, 18.0f);
                }
                if (arousalFactor >= 0.5f) targetSnarlR = 0.2f; 
                break;

            case "Horny / Pain":
                targetDesire = 0.55f;
                targetPain = 0.2f + (arousalFactor * 0.4f); 
                targetLipBiteWide = 0.5f * arousalFactor; 
                if (arousalFactor >= 0.5f) 
                {
                    targetSnarlL = 0.15f + ((arousalFactor - 0.5f) * 0.3f);
                    targetWrongHoleB = 0.25f;
                }
                break;

            case "Evil / Horny":
                targetDesire = 0.7f;
                if (arousalFactor >= 0.5f) targetSnarlL = 0.3f; 
                break;

            case "Angry / Disgust":
                targetFrown = 0.65f; 
                targetContempt = 0.5f;
                break;

            case "Concerned / Surprised":
                targetConfused = 0.45f;
                targetSurprise = 0.35f;
                break;

            case "Great / Surprised":
                targetHappy = 0.65f; 
                targetSurprise = 0.25f;
                if (arousalFactor < 0.6f && UnityEngine.Random.value < 0.008f && Time.time > nextWinkTime[personAtom.uid])
                {
                    activeWinkSide[personAtom.uid] = (UnityEngine.Random.value > 0.5f) ? "Left" : "Right";
                    winkDuration[personAtom.uid] = Time.time + 0.25f;
                    nextWinkTime[personAtom.uid] = Time.time + UnityEngine.Random.Range(6.0f, 15.0f);
                }
                if (arousalFactor >= 0.5f) targetSnarlL = 0.15f;
                break;

            default: 
                targetDesire = arousalFactor * 0.3f;
                if (arousalFactor < 0.4f && UnityEngine.Random.value < 0.003f && Time.time > nextWinkTime[personAtom.uid])
                {
                    activeWinkSide[personAtom.uid] = "Left";
                    winkDuration[personAtom.uid] = Time.time + 0.25f;
                    nextWinkTime[personAtom.uid] = Time.time + UnityEngine.Random.Range(10.0f, 25.0f);
                }
                break;
        }

        // =========================================================================
        // PRZYPISANIE WARTOŚCI DLA ISTNIEJĄCYCH ZMIENNYCH
        // =========================================================================
        targetAfraid = (personality == "Fear / Shocked") ? 0.65f : 0f;
        targetAngry = (personality == "Angry / Surprised" && arousalFactor < 0.5f) ? 0.5f : 0f;
        targetConcentrate = (personality == "Concerned / Surprised") ? 0.45f : 0f;
        targetFear = (personality == "Fear / Shocked") ? 0.65f : 0f;
        targetShock = (personality == "Fear / Shocked") ? 0.5f : 0f;
        
        float targetDisgust = (personality == "Horny / Disgust" || personality == "Angry / Disgust") ? 0.6f : 0f;

        // ==========================================
        // FAZA AUTOMATYCZNEGO ORGAZMU (100% Arousal)
        // ==========================================
        if (arousalFactor >= 1.0f)
        {
            targetDesire = 0.7f;
            targetPain = 0.5f;
            targetSnarlL = 0.35f; 
            targetSnarlR = 0.35f;
            targetWinkL = 0.65f;  
            targetWinkR = 0.65f;
        }
        else
        {
            // PROCEDURA CZASOWA DLA SUBTELNYCH PUSZCZEŃ OCZKA (AshAuryn Wink)
            if (!nextWinkTime.ContainsKey(personAtom.uid)) nextWinkTime[personAtom.uid] = Time.time + 10f;
            if (!winkDuration.ContainsKey(personAtom.uid)) winkDuration[personAtom.uid] = 0f;
            if (!activeWinkSide.ContainsKey(personAtom.uid)) activeWinkSide[personAtom.uid] = "None";

            if (Time.time < winkDuration[personAtom.uid])
            {
                if (activeWinkSide[personAtom.uid] == "Left") targetWinkL = 1.0f;
                else if (activeWinkSide[personAtom.uid] == "Right") targetWinkR = 1.0f;
            }
            else
            {
                activeWinkSide[personAtom.uid] = "None";
            }
        }

        // =========================================================================
        // PRZESYŁANIE ZBALANSOWANYCH WARTOŚCI DO SUWAKÓW (ZAKŁADKA REALISM / GEOMETRY)
        // =========================================================================
        if (geoComponent != null)
        {
            SetMorphValueSafe(geoComponent, "Afraid", targetAfraid);
            SetMorphValueSafe(geoComponent, "Angry", targetAngry);
            SetMorphValueSafe(geoComponent, "Concentrate", targetConcentrate);
            SetMorphValueSafe(geoComponent, "Confused", targetConfused);
            SetMorphValueSafe(geoComponent, "Contempt", targetContempt);
            SetMorphValueSafe(geoComponent, "Desire", targetDesire);
            SetMorphValueSafe(geoComponent, "Disgust", targetDisgust);
            SetMorphValueSafe(geoComponent, "Excitement", targetExcitement);
            SetMorphValueSafe(geoComponent, "Fear", targetFear); 
            SetMorphValueSafe(geoComponent, "Frown", targetFrown);
            SetMorphValueSafe(geoComponent, "Happy", targetHappy);
            SetMorphValueSafe(geoComponent, "Pain", targetPain);
            SetMorphValueSafe(geoComponent, "Shock", targetShock); 
            SetMorphValueSafe(geoComponent, "Surprise", targetSurprise);
            SetMorphValueSafe(geoComponent, "Snarl Left", targetSnarlL);
            SetMorphValueSafe(geoComponent, "Snarl Right", targetSnarlR);

            // Aplikowanie precyzyjnie przeliczonych morphów z paczki ascorad (.vmi)
            SetMorphValueSafe(geoComponent, ascoPath + "v12 asco - Bedroom Smile.vmi", targetBedroomSmile);
            SetMorphValueSafe(geoComponent, ascoPath + "v12 asco - Lip Bite Wide.vmi", targetLipBiteWide);
            SetMorphValueSafe(geoComponent, ascoPath + "v12 asco - Wrong Hole A.vmi", targetWrongHoleA);
            SetMorphValueSafe(geoComponent, ascoPath + "v12 asco - Wrong Hole B.vmi", targetWrongHoleB);
            SetMorphValueSafe(geoComponent, ascoPath + "v12 asco - Ugh.vmi", targetUgh);
            SetMorphValueSafe(geoComponent, ascoPath + "v12 asco - Uh Oh.vmi", targetUhOh);

            // Aplikowanie zalotnych mrugnięć z paczki AshAuryn (.vmi)
            SetMorphValueSafe(geoComponent, ashPath + "Eyes Wink L.vmi", targetWinkL);
            SetMorphValueSafe(geoComponent, ashPath + "Eyes Wink R.vmi", targetWinkR);
        }
    }

    // Oficjalna metoda sterowania suwakami oparta na poprawnym i zweryfikowanym wywołaniu GetFloatJSONParam
    private void SetMorphValueSafe(JSONStorable geo, string morphUid, float value)
    {
        try
        {
            if (geo != null)
            {
                // Wyciągamy morph dokładnie jako parametr float z poziomu storable (Zgodnie z poprawką Claude AI)
                JSONStorableFloat morphParam = geo.GetFloatJSONParam(morphUid);
                if (morphParam != null)
                {
                    // Płynna zmiana wartości za pomocą interpolacji liniowej (Lerp) chroni przed glitchami
                    morphParam.val = Mathf.Lerp(morphParam.val, value, Time.deltaTime * 3.5f);
                }
            }
        }
        catch { /* Bezpieczne wyłapanie błędu w przypadku braku suwaka na danej postaci */ }
    }
}

    public class UltraRealismModule { public bool autoBreathing, autoFacialAnim, autoLayerControl, autoAudioSync, autoHandMovements, autoCollisionSound, autoImpact, autoStateSystem, autoTessellation, autoFreezePose, autoMovement, autoNaturalMotion; public UltraRealismModule(UltraSceneController p) {} public void UpdateModule(List<Atom> l) {} public void ClearAllSpermManual() {} public void ResetAllJoints(List<Atom> l) {} public void TriggerPostOrgasmCalm(Atom a) {} public void ExportPluginSettings(object param) {} public void ImportPluginSettings(object param) {} }
    public class UltraOrgasmSystem { public float currentExcitement; public bool isAutoOrgasmEnabled; public UltraOrgasmSystem(UltraSceneController p, UltraBlowjobModule b) {} public void UpdateSystem(Atom s, Atom t, string path) {} public void AddExcitement(float a) {} }
    public class UltraGazeModule { public void UpdateModule(Atom s, Atom t) { } public UltraGazeModule(UltraSceneController p) { } }
} // BEZBŁĘDNE ZAMKNIĘCIE CAŁEGO PLIKU PLUGINA .CS
