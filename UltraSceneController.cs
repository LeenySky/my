using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UltraScene
{
    public class UltraSceneController : MVRScript
    {
        // REFERENCJE WEWNĘTRZNYCH PODMODUŁÓW LOGICZNYCH KONTROLERA SCENY
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

        // MASZYNA STANÓW I PARAMETRY NAWIGACJI INTERFEJSU GRAFICZNEGO VAM (CZYSTY START W MAIN)
        private string currentTab = "";
        private string currentSubMenu = "Main"; 
        private string selectedTargetName = ""; 
        private GameObject overlayCanvas;
        private Text overlayTextInfo;

        // SYSTEM WYKRYWANIA I BEZWZGLĘDNEGO RESETOWANIA POZYCJI T (T-POSE)
        private List<string> relaxedPersons = new List<string>();

        // MATRYCE DANYCH WYŚWIETLANE NA EKRANIE W FORMIE NIEZALEŻNEGO TEKSTU OSD (PDF PAGE 1)
        private Dictionary<string, float> personArousal = new Dictionary<string, float>();
        private Dictionary<string, string> personMoveTarget = new Dictionary<string, string>();
        private Dictionary<string, string> personPersonality = new Dictionary<string, string>();

        // MAPOWANIE Atom.uid -> "Person N" (ustalane na nowo każdą ramkę w Update(), używane
        // przez systemy automatyczne np. Blowjob do znalezienia właściwego banku audio danej osoby)
        private Dictionary<string, string> personKeyByAtomUid = new Dictionary<string, string>();

        // SYSTEM AUTOMATYCZNEGO BLOWJOB: DETEKCJA BLISKOŚCI + TEMPO Z VELOCITY + NATYWNY AUTO-JAW VAM
        // UWAGA: to PRZYBLIŻENIE bliskości (hipControl dawcy <-> headControl odbiorcy), nie precyzyjna
        // kolizja anatomiczna - VaM nie udostępnia pluginom nazw kolizderów genitaliów/ust do referencji.
        private const float BlowjobProximityThreshold = 0.15f;
        private Dictionary<string, float> blowjobNextPlayTime = new Dictionary<string, float>();
        private HashSet<string> blowjobJawActiveUids = new HashSet<string>();

        // BANKI AUDIO (WIELE PLIKÓW NA SLOT) - PER POSTAĆ I JEDEN SLOT NA ASSET
        private Dictionary<string, AudioBank> personAudioBanks = new Dictionary<string, AudioBank>();
        private AudioBank assetAudioBank;
        private bool audioBanksRestored = false;

        // KATEGORIE AUDIO O IDENTYCZNEJ STRUKTURZE (Import/Clear/Volume/Amplification/Pitch/Interval/3D Audio,
        // BEZ Morph) - PER POSTAĆ. SPŁASZCZONY słownik (klucz = "Kategoria|Person N") - zagnieżdżony
        // Dictionary<string, Dictionary<...>> wywala wewnętrzny błąd starego kompilatora Mono w VaM (CS584).
        private Dictionary<string, AudioBank> simpleAudioBanks = new Dictionary<string, AudioBank>();
        private static readonly string[] SimpleAudioCategories = { "Breathing", "Licking", "Oral", "Thrust", "Orgasm", "Handjob" };
        private Dictionary<string, string> simpleAudioCategoryPrefix = new Dictionary<string, string>
        {
            { "Breathing", "AudBr" },
            { "Licking", "AudLk" },
            { "Oral", "AudOl" },
            { "Thrust", "AudTh" },
            { "Orgasm", "AudOg" },
            { "Handjob", "AudHj" }
        };

        // SLAPS: JEDEN GLOBALNY BANK (nie per-postać)
        private AudioBank slapsAudioBank;

        // DOMYŚLNY KATALOG OTWIERANY PRZY IMPORCIE AUDIO (ścieżka względna do katalogu głównego VaM)
        private const string DefaultAudioFolder = "Custom/Sounds/UltraSceneController";

        // SSS PATTERN: JEDYNY REJESTR ELEMENTÓW AKTUALNIE NA EKRANIE.
        // Zamiast list Transformów przełączanych SetActive(), trzymamy delegaty "jak to posprzątać"
        // dla elementów stworzonych w OSTATNIM renderze. Przed każdym kolejnym renderem są fizycznie
        // usuwane (RemoveButton/RemoveToggle/...), więc VaM nie ma czego "wymusić widocznym".
        private List<Action> activeDynamicElements = new List<Action>();

        // Pomocniczy stan nawigacji: która kategoria Audio jest aktualnie otwarta
        // ("Persons", "Assets", "Breathing", "Licking", "Oral", "Thrust", "Orgasm", "Handjob", "Slaps")
        private string audioCategory = "";

        // SŁOWNIKI GLOBALNYCH PARAMETRÓW KONTROLEK INTERFEJSU (WZORZEC SSS / TCODE)
        private Dictionary<string, JSONStorableBool> pluginBools = new Dictionary<string, JSONStorableBool>();
        private Dictionary<string, JSONStorableFloat> pluginFloats = new Dictionary<string, JSONStorableFloat>();
        private Dictionary<string, JSONStorableStringChooser> personalityStorables = new Dictionary<string, JSONStorableStringChooser>();
        private Dictionary<string, JSONStorableStringChooser> moveStorables = new Dictionary<string, JSONStorableStringChooser>();
        private Dictionary<string, JSONStorableFloat> arousalStorables = new Dictionary<string, JSONStorableFloat>();

        private JSONStorableUrl audioUrlParam;
        private JSONStorableUrl assetUrlParam;
        // JEDNORAZOWA REJESTRACJA PARAMETRÓW LOGICZNYCH W RAM (WZORZEC SSS)
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

                // PRZEDREJESTRACJA STRUKTUR DANYCH DLA 4 POSTACI (PDF PAGE 1)
                for (int i = 1; i <= 4; i++)
                {
                    string key = "Person " + i;
                    personPersonality[key] = "Neutral";
                    personMoveTarget[key] = "None";
                    personArousal[key] = 0.0f;

                    var pChoice = new JSONStorableStringChooser("Person " + i + " Personality", 
                        new List<string> { "Neutral", "Happy", "Angry", "Fear", "Surprise", "Disgust", "Ectasy", "Sensual", "Shy", "Tired" }, 
                        "Neutral", "Personality", (string val) => { if(!string.IsNullOrEmpty(val)) personPersonality[key] = val; });
                    RegisterStringChooser(pChoice); personalityStorables[key] = pChoice;

                    var mChoice = new JSONStorableStringChooser("Person " + i + " Move Target", 
                        new List<string> { "None", "Camera", "Person 1", "Person 2", "Person 3", "Person 4" }, 
                        "None", "Move Target", (string val) => { if(!string.IsNullOrEmpty(val)) personMoveTarget[key] = val; });
                    RegisterStringChooser(mChoice); moveStorables[key] = mChoice;

                    var aSlider = new JSONStorableFloat("Person " + i + " Arousal Level", 0f, (float val) => { personArousal[key] = val; }, 0f, 100f, true);
                    RegisterFloat(aSlider); arousalStorables[key] = aSlider;

                    var audioPathsStorable = new JSONStorableString("Person " + i + " Audio Files", "");
                    RegisterString(audioPathsStorable);
                    personAudioBanks[key] = new AudioBank(audioPathsStorable);
                }

                var assetAudioPathsStorable = new JSONStorableString("Asset Audio Files", "");
                RegisterString(assetAudioPathsStorable);
                assetAudioBank = new AudioBank(assetAudioPathsStorable);

                // 3D AUDIO DLA PERSONS / ASSETS (parametry dźwięku przestrzennego)
                pluginBools["Aud3D"] = new JSONStorableBool("Persons 3D Audio", false);
                pluginBools["As3D"] = new JSONStorableBool("Assets 3D Audio", false);
                RegisterBool(pluginBools["Aud3D"]);
                RegisterBool(pluginBools["As3D"]);

                // SLAPS: JEDEN GLOBALNY BANK (nie per-postać)
                pluginFloats["AudSlVolume"] = new JSONStorableFloat("Slaps Volume", 0.8f, 0f, 1f, true);
                pluginFloats["AudSlAmplification"] = new JSONStorableFloat("Slaps Amplification", 1.0f, 0f, 3f, true);
                pluginFloats["AudSlPitch"] = new JSONStorableFloat("Slaps Pitch", 1.0f, 0f, 3f, true);
                pluginFloats["AudSlInterval"] = new JSONStorableFloat("Slaps Interval Speed", 1.0f, 0f, 3f, true);
                pluginBools["AudSl3D"] = new JSONStorableBool("Slaps 3D Audio", false);
                RegisterFloat(pluginFloats["AudSlVolume"]);
                RegisterFloat(pluginFloats["AudSlAmplification"]);
                RegisterFloat(pluginFloats["AudSlPitch"]);
                RegisterFloat(pluginFloats["AudSlInterval"]);
                RegisterBool(pluginBools["AudSl3D"]);
                var slapsPathsStorable = new JSONStorableString("Slaps Audio Files", "");
                RegisterString(slapsPathsStorable);
                slapsAudioBank = new AudioBank(slapsPathsStorable);

                // 6 KATEGORII O IDENTYCZNEJ STRUKTURZE (BEZ Morph) - PER POSTAĆ:
                // Breathing, Licking, Oral, Thrust, Orgasm, Handjob
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

                // 1. BEZBŁĘDNA REJESTRACJA PRZEŁĄCZNIKÓW Z ANGIELSKIMI NAZWAMI JAKO IDENTYFIKATORY
                pluginBools["HJEnableLeft"] = new JSONStorableBool("Enable Left Hand", false);
                pluginBools["HJEnableRight"] = new JSONStorableBool("Enable Right Hand", false);
                pluginBools["BREnable"] = new JSONStorableBool("Enable Anatomical Breathing", true);
                pluginBools["BJEnable"] = new JSONStorableBool("Enable Oral Control", false);
                pluginBools["BJAuto"] = new JSONStorableBool("Auto Mouth Sync", false);
                pluginBools["PNEnable"] = new JSONStorableBool("Enable Anatomy Reaction", false);
                pluginBools["GZEnable"] = new JSONStorableBool("Enable Gaze System", true);
                
                pluginBools["RLAutoBreathing"] = new JSONStorableBool("Auto Breathing", true);
                pluginBools["RLAutoLicking"] = new JSONStorableBool("Auto Licking", true);
                pluginBools["RLAutoForeskin"] = new JSONStorableBool("Auto Foreskin", false);
                pluginBools["RLAutoSucking"] = new JSONStorableBool("Auto Sucking", true);
                pluginBools["RLAutoHandMovements"] = new JSONStorableBool("Auto Hand Movements", false);
                pluginBools["RLAutoPenetrationSound"] = new JSONStorableBool("Auto Penetration Sound", true);
                pluginBools["RLAutoSlap"] = new JSONStorableBool("Auto Slap Physics", false);
                pluginBools["RLAutoOrgasm"] = new JSONStorableBool("Auto Orgasm System", true);
                pluginBools["RLAutoTessellation"] = new JSONStorableBool("Auto Mesh Tessellation", false);
                pluginBools["RLAutoFreezePose"] = new JSONStorableBool("Auto Freeze Pose", true);
                pluginBools["RLAutoMovement"] = new JSONStorableBool("Auto Micro Movement", false);
                pluginBools["RLAutoNaturalMotion"] = new JSONStorableBool("Auto Natural Motion", true);

                // Kuloodporna rejestracja przez bezpieczną tablicę kluczy (Eradykacja błędu CS584)
                string[] boolKeysToRegister = { 
                    "HJEnableLeft", "HJEnableRight", "BREnable", "BJEnable", "BJAuto", "PNEnable", "GZEnable", 
                    "RLAutoBreathing", "RLAutoLicking", "RLAutoForeskin", "RLAutoSucking", 
                    "RLAutoHandMovements", "RLAutoPenetrationSound", "RLAutoSlap", "RLAutoOrgasm", 
                    "RLAutoTessellation", "RLAutoFreezePose", "RLAutoMovement", "RLAutoNaturalMotion" 
                };
                foreach (string bk in boolKeysToRegister) { if (pluginBools.ContainsKey(bk)) RegisterBool(pluginBools[bk]); }

                // 2. BEZBŁĘDNA REJESTRACJA SUWAKÓW Z ANGIELSKIMI NAZWAMI JAKO IDENTYFIKATORY
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
                pluginFloats["BJVelocity"] = new JSONStorableFloat("Oral Velocity", 1.0f, 0f, 3f, true);
                pluginFloats["BJIntense"] = new JSONStorableFloat("Oral Intensity", 1.0f, 0f, 2f, true);
                pluginFloats["PNVelocity"] = new JSONStorableFloat("Reaction Velocity", 1.0f, 0f, 3f, true);
                pluginFloats["PNIntense"] = new JSONStorableFloat("Reaction Intensity", 1.0f, 0f, 2f, true);
                
                pluginFloats["GZHeadWeight"] = new JSONStorableFloat("Head Look Weight", 0.7f, 0f, 1f, true);
                pluginFloats["GZChestWeight"] = new JSONStorableFloat("Chest Look Weight", 0.7f, 0f, 1f, true);

                string[] floatKeysToRegister = { 
                    "AudVolume", "AudAmplification", "AudPitch", "AudInterval", "AsVolume", "AsAmplification", 
                    "AsPitch", "AsInterval", "HJVelLeft", "HJVelRight", "HJIntLeft", "HJIntRight", 
                    "BRVelocity", "BRIntense", "BJVelocity", "BJIntense", "PNVelocity", "PNIntense", 
                    "GZHeadWeight", "GZChestWeight" 
                };
                foreach (string fk in floatKeysToRegister) { if (pluginFloats.ContainsKey(fk)) RegisterFloat(pluginFloats[fk]); }

                InitStorableParameters();
                CreateScreenOverlayUI();
                CreateDynamicLayoutUI();
            }
            catch (Exception e) { SuperController.LogError("Init Error: " + e.Message); }
        }
        private void InitStorableParameters()
        {
            RegisterBool(new JSONStorableBool("Enable Audio", true));
            RegisterBool(new JSONStorableBool("Enable Breathing", true));
            RegisterBool(new JSONStorableBool("Enable Handjob", false));
            RegisterBool(new JSONStorableBool("Enable Blowjob", false));
            RegisterFloat(new JSONStorableFloat("Master Volume", 0.8f, 0f, 1f));
            RegisterFloat(new JSONStorableFloat("Movement Speed", 1.0f, 0.1f, 3.0f));
        }

        // AKTYWNA PĘTLA SILNIKA FIZYKI I MONITOROWANIA STANU SCENY (ANGIELSKIE OSD)
        public void Update()
        {
            try
            {
                if (SuperController.singleton == null) return;

                // Banki audio trzeba odbudować z zapisanych UID PO tym, jak VaM przywróci wartości
                // storable'i ze sceny (Init() tworzy puste banki - dane wczytania scenariusza nadpisują
                // je dopiero po Init(), więc odbudowujemy raz, gdy scena faktycznie skończy się ładować).
                if (!audioBanksRestored && !SuperController.singleton.isLoading)
                {
                    audioBanksRestored = true;
                    foreach (AudioBank bank in personAudioBanks.Values) bank.RebuildFromStorable();
                    if (assetAudioBank != null) assetAudioBank.RebuildFromStorable();
                    if (slapsAudioBank != null) slapsAudioBank.RebuildFromStorable();
                    foreach (AudioBank bank in simpleAudioBanks.Values) bank.RebuildFromStorable();
                }

                List<Atom> allAtoms = SuperController.singleton.GetAtoms();
                if (allAtoms == null) return;

                string osdText = "ULTRA SCENE CONTROLLER V2\n---------------------------\n";
                int pCount = 1;

                foreach (Atom atom in allAtoms)
                {
                    if (atom != null && atom.type == "Person")
                    {
                        string pName = atom.name;
                        string key = "Person " + pCount;

                        // CAŁKOWITE UWOLNIENIE Z POZYCJI T-POSE W SILNIKU FIZYKI
                        if (!relaxedPersons.Contains(pName) && pCount <= 4)
                        {
                            JSONStorable physicsStorable = atom.GetStorableByID("Physics");
                            if (physicsStorable != null) physicsStorable.CallAction("Simulate");

                            string[] targets = { "hipControl", "chestControl", "headControl", "leftHandControl", "rightHandControl", "leftFootControl", "rightFootControl" };
                            foreach (string ctrlName in targets)
                            {
                                JSONStorable ctrl = atom.GetStorableByID(ctrlName);
                                if (ctrl != null) ctrl.CallAction("ON");
                            }

                            Rigidbody[] rbs = atom.GetComponentsInChildren<Rigidbody>();
                            if (rbs != null) {
                                foreach (Rigidbody rb in rbs) {
                                    if (rb != null && (rb.name.Contains("Hand") || rb.name.Contains("Arm"))) {
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
                            osdText += "Character " + pCount + ": " + pName.ToUpper() + "\n" +
                                       "   Personality: " + personPersonality[key] + "\n" +
                                       "   Arousal Level: " + personArousal[key].ToString("F1") + "%\n" +
                                       "   Movement Target: " + personMoveTarget[key] + "\n\n";
                        }
                        pCount++;
                    }
                }
                if (pCount == 1) osdText += "(No characters detected in the VaM scene)\n";
                if (overlayTextInfo != null) overlayTextInfo.text = osdText;

                // SYSTEM AUTOMATYCZNEGO BLOWJOB: kolizja(bliskość)/start sekwencji + tempo z Velocity
                JSONStorableBool bjEnable = GetBool("BJEnable");
                if (bjEnable != null && bjEnable.val)
                {
                    UpdateBlowjobSystem(allAtoms);
                }
                else if (blowjobJawActiveUids.Count > 0)
                {
                    foreach (string uid in blowjobJawActiveUids) SetAutoJaw(SuperController.singleton.GetAtomByUid(uid), false);
                    blowjobJawActiveUids.Clear();
                }
            }
            catch { }
        }

        public void OnDestroy() { if (overlayCanvas != null) UnityEngine.Object.Destroy(overlayCanvas); }

        // =========================================================================
        // SYSTEM BLOWJOB: bliskość startuje sekwencję, Velocity ustala tempo,
        // BJAuto włącza natywny mechanizm VaM auto-jaw (jak w VAMMoan) na odbiorcy.
        // =========================================================================
        private void UpdateBlowjobSystem(List<Atom> allAtoms)
        {
            JSONStorableBool autoToggle = GetBool("BJAuto");
            bool autoEnabled = (autoToggle != null && autoToggle.val);
            HashSet<string> inRangeNow = new HashSet<string>();

            foreach (Atom giver in allAtoms)
            {
                if (giver == null || giver.type != "Person") continue;
                DAZCharacterSelector giverGeo = giver.GetStorableByID("geometry") as DAZCharacterSelector;
                if (giverGeo == null || giverGeo.gender != DAZCharacterSelector.Gender.Male) continue;
                FreeControllerV3 giverHip = giver.GetStorableByID("hipControl") as FreeControllerV3;
                if (giverHip == null) continue;

                foreach (Atom receiver in allAtoms)
                {
                    if (receiver == null || receiver == giver || receiver.type != "Person") continue;
                    FreeControllerV3 receiverHead = receiver.GetStorableByID("headControl") as FreeControllerV3;
                    if (receiverHead == null) continue;

                    float dist = Vector3.Distance(giverHip.transform.position, receiverHead.transform.position);
                    if (dist > BlowjobProximityThreshold) continue;

                    inRangeNow.Add(receiver.uid);
                    PlayBlowjobBeat(receiver);
                }
            }

            // Włącz auto-jaw nowym odbiorcom w zasięgu (jeśli BJAuto aktywne)
            foreach (string uid in inRangeNow)
            {
                if (autoEnabled && !blowjobJawActiveUids.Contains(uid))
                {
                    SetAutoJaw(SuperController.singleton.GetAtomByUid(uid), true);
                    blowjobJawActiveUids.Add(uid);
                }
            }

            // Wyłącz auto-jaw tym, którzy wypadli z zasięgu albo gdy BJAuto wyłączono
            List<string> toTurnOff = new List<string>();
            foreach (string uid in blowjobJawActiveUids)
            {
                if (!autoEnabled || !inRangeNow.Contains(uid)) toTurnOff.Add(uid);
            }
            foreach (string uid in toTurnOff)
            {
                SetAutoJaw(SuperController.singleton.GetAtomByUid(uid), false);
                blowjobJawActiveUids.Remove(uid);
            }
        }

        // Odtwarza kolejny "beat" z banku audio Oral odbiorcy w tempie wyznaczonym przez suwak Velocity.
        private void PlayBlowjobBeat(Atom receiver)
        {
            if (receiver == null) return;
            string uid = receiver.uid;
            float now = Time.time;
            float nextTime;
            if (!blowjobNextPlayTime.TryGetValue(uid, out nextTime)) nextTime = 0f;
            if (now < nextTime) return;

            JSONStorableFloat velocityStorable = GetFloat("BJVelocity");
            float velocity = (velocityStorable != null) ? velocityStorable.val : 1f;
            blowjobNextPlayTime[uid] = now + (1f / Mathf.Max(0.05f, velocity));

            string key;
            if (!personKeyByAtomUid.TryGetValue(uid, out key)) return;
            AudioBank bank;
            if (!simpleAudioBanks.TryGetValue("Oral|" + key, out bank) || bank == null) return;
            NamedAudioClip clip = bank.GetRandomClip();
            if (clip == null) return;

            AudioSourceControl receiverAudio = receiver.GetStorableByID("HeadAudioSource") as AudioSourceControl;
            if (receiverAudio != null) receiverAudio.PlayNow(clip);
        }

        // Przełącza natywny system VaM "auto-jaw" (ten sam mechanizm, na którym opiera się VAMMoan) -
        // usta poruszają się automatycznie zgodnie z aktualnie odtwarzanym dźwiękiem danej osoby.
        private void SetAutoJaw(Atom atom, bool enabled)
        {
            if (atom == null) return;
            JSONStorable jawControl = atom.GetStorableByID("JawControl");
            if (jawControl != null) jawControl.SetBoolParamValue("driveXRotationFromAudioSource", enabled);
            JSONStorable autoJawMouthMorph = atom.GetStorableByID("AutoJawMouthMorph");
            if (autoJawMouthMorph != null) autoJawMouthMorph.SetBoolParamValue("enabled", enabled);
        }

        // =========================================================================
        // SSS PATTERN: 100% PROCEDURALNE, NA-ŻĄDANIE BUDOWANE UI (BEZ LIST + SETACTIVE)
        // =========================================================================
        // Dane (JSONStorableBool/Float/StringChooser w pluginBools/pluginFloats/...) żyją
        // przez cały czas życia pluginu i są tworzone tylko raz w Init(). Ta metoda TYLKO
        // renderuje WIDOKI dla aktualnego stanu nawigacji. Każde wejście fizycznie usuwa
        // poprzedni render (Remove*), więc VaM nie ma żadnych "martwych" elementów do
        // wymuszenia widocznymi przy "Open Custom UI...".
        private void CreateDynamicLayoutUI()
        {
            try
            {
                ClearDynamicLayout();

                bool isMain = (currentSubMenu == "Main");

                AddHeaderLabel(BuildHeaderText(), Color.white);
                if (!isMain) AddBackButton();

                switch (currentSubMenu)
                {
                    case "Audio_Home":
                        RenderAudioHome();
                        break;
                    case "Audio_PersonPicker":
                        RenderPersonPicker((key) => { selectedTargetName = key; currentSubMenu = "Execute"; CreateDynamicLayoutUI(); });
                        break;
                    case "Audio_Assets":
                        RenderAudioAssets();
                        break;
                    case "Select_Person":
                        RenderPersonPicker((key) => { selectedTargetName = key; currentSubMenu = "Execute"; CreateDynamicLayoutUI(); });
                        break;
                    case "Execute":
                        RenderExecutePanel();
                        break;
                    default:
                        RenderMainMenu();
                        break;
                }
            }
            catch (Exception e) { SuperController.LogError("UI Build Error: " + e.Message); }
        }

        // Fizycznie usuwa WSZYSTKIE elementy stworzone w poprzednim renderze (nigdy SetActive)
        private void ClearDynamicLayout()
        {
            foreach (Action cleanup in activeDynamicElements)
            {
                try { cleanup(); } catch { }
            }
            activeDynamicElements.Clear();
        }

        private string BuildHeaderText()
        {
            string loadedName = string.IsNullOrEmpty(currentTab) ? "MAIN INTERFACE" : currentTab.ToUpper();
            if (currentSubMenu == "Audio_Home") loadedName = "AUDIO CATEGORIES";
            else if (currentSubMenu == "Audio_PersonPicker") loadedName = "SELECT CHARACTER (" + audioCategory.ToUpper() + ")";
            else if (currentSubMenu == "Select_Person") loadedName = "SELECT CHARACTER";
            else if (currentSubMenu == "Audio_Assets") loadedName = "SELECT CUSTOM UNITY ASSET (CUA)";
            else if (currentSubMenu == "Execute")
            {
                if (currentTab == "Audio") loadedName = "AUDIO: " + audioCategory.ToUpper() + (string.IsNullOrEmpty(selectedTargetName) ? "" : " - " + selectedTargetName.ToUpper());
                else loadedName = "PANEL: " + (string.IsNullOrEmpty(selectedTargetName) ? currentTab.ToUpper() : selectedTargetName.ToUpper());
            }
            return "=== " + loadedName + " ===";
        }

        // -------------------------------------------------------------------
        // NISKOPOZIOMOWE FABRYKI ELEMENTÓW UI: KAŻDA REJESTRUJE WŁASNY "DESTRUKTOR"
        // -------------------------------------------------------------------
        private UIDynamicButton AddButton(string label, bool rightSide, Color color, UnityEngine.Events.UnityAction onClick = null, bool interactable = true)
        {
            UIDynamicButton b = CreateButton(label, rightSide);
            if (b == null) return null;
            b.button.interactable = interactable;
            SetBtnColor(b, color);
            if (onClick != null) b.button.onClick.AddListener(onClick);
            activeDynamicElements.Add(() => RemoveButton(b));
            return b;
        }

        // VaM renderuje dwie NIEZALEŻNE kolumny (lewa/prawa), każda zajmuje pół szerokości panelu -
        // nie ma udokumentowanego sposobu na "rozciągnięcie" jednego elementu przez obie kolumny
        // bez ryzykownego grzebania w wewnętrznym RectTransform/LayoutGroup. Zamiast zgadywać,
        // tworzymy IDENTYCZNY baner w obu kolumnach naraz - efekt wizualny to ciągły tytuł na całą
        // szerokość okna, a kod korzysta tylko ze sprawdzonego, publicznego API.
        private void AddHeaderLabel(string text, Color color)
        {
            UIDynamicButton hdrLeft = CreateButton(text, false);
            if (hdrLeft != null)
            {
                hdrLeft.button.interactable = false;
                SetBtnColor(hdrLeft, color);
                activeDynamicElements.Add(() => RemoveButton(hdrLeft));
            }

            UIDynamicButton hdrRight = CreateButton(text, true);
            if (hdrRight != null)
            {
                hdrRight.button.interactable = false;
                SetBtnColor(hdrRight, color);
                activeDynamicElements.Add(() => RemoveButton(hdrRight));
            }
        }

        private void AddBackButton()
        {
            AddButton("<-- Back", false, Color.gray, () => { GoBackMenuLogic(); });
        }

        private void AddSubHeader(string text, Color c, bool rightSide)
        {
            string id = "hdr_" + Guid.NewGuid().ToString("N");
            JSONStorableString storage = new JSONStorableString(id, text);
            UIDynamicTextField fld = CreateTextField(storage, rightSide);
            if (fld == null) return;
            fld.height = 35f;
            Text txt = fld.GetComponentInChildren<Text>();
            if (txt != null) { txt.color = c; txt.alignment = TextAnchor.MiddleCenter; }
            Image img = fld.GetComponent<Image>();
            if (img != null) img.color = new Color(0.04f, 0.04f, 0.06f);
            activeDynamicElements.Add(() => RemoveTextField(fld));
        }

        private void AddToggle(JSONStorableBool storable, bool rightSide)
        {
            if (storable == null) return;
            UIDynamicToggle t = CreateToggle(storable, rightSide);
            if (t != null) activeDynamicElements.Add(() => RemoveToggle(t));
        }

        private void AddSlider(JSONStorableFloat storable, bool rightSide, Color? txtColor = null)
        {
            if (storable == null) return;
            UIDynamicSlider s = CreateSlider(storable, rightSide);
            if (s == null) return;
            if (txtColor.HasValue)
            {
                Text txt = s.GetComponentInChildren<Text>();
                if (txt != null) txt.color = txtColor.Value;
            }
            activeDynamicElements.Add(() => RemoveSlider(s));
        }

        private void AddPopup(JSONStorableStringChooser storable, bool rightSide)
        {
            if (storable == null) return;
            UIDynamicPopup p = CreatePopup(storable, rightSide);
            if (p != null) activeDynamicElements.Add(() => RemovePopup(p));
        }

        // Kuloodporny odczyt słowników (zamiast pluginFloats["x"] który wybucha gdy brak klucza)
        private JSONStorableFloat GetFloat(string key) { JSONStorableFloat f; return pluginFloats.TryGetValue(key, out f) ? f : null; }
        private JSONStorableBool GetBool(string key) { JSONStorableBool b; return pluginBools.TryGetValue(key, out b) ? b : null; }

        // -------------------------------------------------------------------
        // RENDEROWANIE EKRANÓW: TYLKO TO, CO POTRZEBNE TERAZ, NIC WIĘCEJ
        // -------------------------------------------------------------------
        private void RenderMainMenu()
        {
            Color cyan = new Color(0f, 0.55f, 0.70f);
            string[] tabs = { "Audio", "Handjob", "Breathing", "Blowjob", "Penetration", "Expression", "Gaze", "Realism" };
            string[] lbls = { "AUDIO", "HANDJOB", "BREATHING", "BLOWJOB", "PENETRATION", "EXPRESSION", "GAZE & GLANCE", "REALISM" };

            // SIATKA 2x4: i%2==0 -> kolumna lewa, i%2==1 -> kolumna prawa (4 wiersze x 2 kolumny)
            for (int i = 0; i < tabs.Length; i++)
            {
                string t = tabs[i];
                bool isRight = (i % 2 != 0);
                AddButton(lbls[i], isRight, cyan, () =>
                {
                    currentTab = t;
                    selectedTargetName = "";
                    audioCategory = "";
                    currentSubMenu = (t == "Audio") ? "Audio_Home" : ((t == "Breathing" || t == "Penetration" || t == "Gaze") ? "Select_Person" : "Execute");
                    CreateDynamicLayoutUI();
                });
            }
        }

        private void RenderAudioHome()
        {
            string[] categories = { "Persons", "Assets", "Breathing", "Licking", "Oral", "Thrust", "Orgasm", "Handjob", "Slaps" };
            Color[] categoryColors = {
                new Color(0.0f, 0.4f, 0.8f),   // Persons - blue
                new Color(0.0f, 0.6f, 0.3f),   // Assets - green
                new Color(0.05f, 0.5f, 0.65f), // Breathing - cyan
                new Color(0.5f, 0.35f, 0.1f),  // Licking - brown
                new Color(0.45f, 0.1f, 0.65f), // Oral - purple
                new Color(0.65f, 0.05f, 0.05f),// Thrust - red
                new Color(0.8f, 0.4f, 0.0f),   // Orgasm - orange
                new Color(0.85f, 0.1f, 0.45f), // Handjob - pink
                new Color(0.4f, 0.4f, 0.4f)    // Slaps - gray
            };

            for (int i = 0; i < categories.Length; i++)
            {
                string cat = categories[i];
                bool isRight = (i % 2 != 0);
                AddButton(cat.ToUpper(), isRight, categoryColors[i], () =>
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

        // Wspólny ekran wyboru postaci - używany przez Audio_Persons i Select_Person (Breathing/Penetration/Gaze)
        private void RenderPersonPicker(Action<string> onSelect)
        {
            Color slate = new Color(0.3f, 0.3f, 0.35f);
            for (int i = 1; i <= 4; i++)
            {
                string key = "Person " + i;
                bool isRight = (i % 2 == 0);
                string label = key + " (" + personPersonality[key] + ")";
                AddButton(label, isRight, slate, () => { onSelect(key); });
            }
        }

        private void RenderAudioAssets()
        {
            // TODO: podłączyć pod realne atomy "CustomUnityAsset" wykryte w scenie, analogicznie
            // do RenderPersonPicker dla Person. Na razie pojedynczy generyczny slot na asset.
            AddSubHeader("No assets configured yet", Color.gray, false);
            AddButton("USE GENERIC ASSET SLOT", false, new Color(0.0f, 0.6f, 0.3f), () =>
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
                case "Slaps": RenderSlapsAudioExecute(); break;
                default: RenderSimpleCategoryAudioExecute(audioCategory); break; // Breathing/Licking/Oral/Thrust/Orgasm/Handjob
            }
        }

        private void RenderPersonsAudioExecute()
        {
            Color cyan = new Color(0f, 0.55f, 0.70f);
            AddSlider(GetFloat("AudVolume"), false, cyan);
            AddSlider(GetFloat("AudAmplification"), true, cyan);
            AddSlider(GetFloat("AudPitch"), false, cyan);
            AddSlider(GetFloat("AudInterval"), true, cyan);
            AddToggle(GetBool("Aud3D"), false);

            AudioBank bank = personAudioBanks.ContainsKey(selectedTargetName) ? personAudioBanks[selectedTargetName] : null;
            RenderAudioFilePicker(bank);
        }

        private void RenderAssetsAudioExecute()
        {
            Color cyan = new Color(0f, 0.55f, 0.70f);
            AddSlider(GetFloat("AsVolume"), false, cyan);
            AddSlider(GetFloat("AsAmplification"), true, cyan);
            AddSlider(GetFloat("AsPitch"), false, cyan);
            AddSlider(GetFloat("AsInterval"), true, cyan);
            AddToggle(GetBool("As3D"), false);

            RenderAudioFilePicker(assetAudioBank);
        }

        private void RenderSlapsAudioExecute()
        {
            Color gray = new Color(0.5f, 0.5f, 0.5f);
            AddSlider(GetFloat("AudSlVolume"), false, gray);
            AddSlider(GetFloat("AudSlAmplification"), true, gray);
            AddSlider(GetFloat("AudSlPitch"), false, gray);
            AddSlider(GetFloat("AudSlInterval"), true, gray);
            AddToggle(GetBool("AudSl3D"), false);

            RenderAudioFilePicker(slapsAudioBank);
        }

        // Wspólny renderer dla Breathing/Licking/Oral/Thrust/Orgasm/Handjob - identyczna struktura,
        // różni się tylko prefiksem parametrów i tym, który bank per-postać jest aktywny.
        private void RenderSimpleCategoryAudioExecute(string category)
        {
            if (!simpleAudioCategoryPrefix.ContainsKey(category)) return;
            string prefix = simpleAudioCategoryPrefix[category];
            Color color = new Color(0.5f, 0.5f, 0.1f);

            AddSlider(GetFloat(prefix + "Volume"), false, color);
            AddSlider(GetFloat(prefix + "Amplification"), true, color);
            AddSlider(GetFloat(prefix + "Pitch"), false, color);
            AddSlider(GetFloat(prefix + "Interval"), true, color);
            AddToggle(GetBool(prefix + "3D"), false);

            AudioBank bank;
            simpleAudioBanks.TryGetValue(category + "|" + selectedTargetName, out bank);
            RenderAudioFilePicker(bank);
        }

        // PRZYCISKI IMPORTU .wav / .ogg / .mp3 DLA AKTUALNEGO BANKU (OSOBA LUB ASSET)
        // Realny import CAŁEGO KATALOGU jednym kliknięciem + dowolnej liczby pojedynczych plików,
        // oparty na potwierdzonym, sandbox-bezpiecznym API VaM (bez System.IO):
        //   SuperController.singleton.GetDirectoryPathDialog(...) -> wybór folderu
        //   SuperController.singleton.GetFilesAtPath(...)         -> lista plików w folderze
        //   URLAudioClipManager.singleton.QueueClip(...)          -> wczytanie klipu audio
        private void RenderAudioFilePicker(AudioBank bank)
        {
            if (bank == null) return;

            Color gold = new Color(0.8f, 0.7f, 0.1f);
            string summary = (bank.Clips.Count == 0) ? "(no files loaded)" : (bank.Clips.Count + " file(s) loaded");
            AddSubHeader("Audio Bank: " + summary, gold, false);

            AddButton("IMPORT FOLDER", false, new Color(0.0f, 0.55f, 0.35f), () =>
            {
                SuperController.singleton.GetDirectoryPathDialog((string path) =>
                {
                    if (!string.IsNullOrEmpty(path)) bank.ImportFolder(path);
                    CreateDynamicLayoutUI();
                }, DefaultAudioFolder);
            });

            AddButton("IMPORT FILE", true, new Color(0.0f, 0.45f, 0.60f), () =>
            {
                SuperController.singleton.GetMediaPathDialog((string path) =>
                {
                    if (!string.IsNullOrEmpty(path)) bank.ImportFile(path);
                    CreateDynamicLayoutUI();
                }, "wav|ogg|mp3", DefaultAudioFolder);
            });

            AddButton("CLEAR ALL FILES", false, new Color(0.5f, 0.1f, 0.1f), () =>
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
            AddToggle(GetBool("BREnable"), false);
            AddSlider(GetFloat("BRVelocity"), false, breathingCyan);
            AddSlider(GetFloat("BRIntense"), true, breathingCyan);
        }

        private void RenderBlowjobExecute()
        {
            Color darkPurple = new Color(0.45f, 0.1f, 0.65f);
            AddToggle(GetBool("BJEnable"), false);
            AddToggle(GetBool("BJAuto"), true);
            AddSlider(GetFloat("BJVelocity"), false, darkPurple);
            AddSlider(GetFloat("BJIntense"), true, darkPurple);
        }

        private void RenderPenetrationExecute()
        {
            Color deepRed = new Color(0.65f, 0.05f, 0.05f);
            AddToggle(GetBool("PNEnable"), false);
            AddSlider(GetFloat("PNVelocity"), false, deepRed);
            AddSlider(GetFloat("PNIntense"), true, deepRed);
        }

        private void RenderGazeExecute()
        {
            Color darkEmerald = new Color(0.02f, 0.40f, 0.20f);
            AddToggle(GetBool("GZEnable"), false);
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

                AddSubHeader(key, violet, rightSide);
                if (personalityStorables.ContainsKey(key)) AddPopup(personalityStorables[key], rightSide);
                if (arousalStorables.ContainsKey(key)) AddSlider(arousalStorables[key], rightSide, violet);
                if (moveStorables.ContainsKey(key)) AddPopup(moveStorables[key], rightSide);
            }
        }

        // PANEL REALISM: WYMAGANYCH 12 PRZEŁĄCZNIKÓW AUTOMATYZACJI (MAPOWANIE 1:1 NA UltraRealismModule)
        private void RenderRealismExecute()
        {
            string[] realismKeys = {
                "RLAutoBreathing", "RLAutoLicking", "RLAutoForeskin", "RLAutoSucking",
                "RLAutoHandMovements", "RLAutoPenetrationSound", "RLAutoSlap", "RLAutoOrgasm",
                "RLAutoTessellation", "RLAutoFreezePose", "RLAutoMovement", "RLAutoNaturalMotion"
            };
            for (int i = 0; i < realismKeys.Length; i++)
            {
                bool isRight = (i % 2 != 0);
                AddToggle(GetBool(realismKeys[i]), isRight);
            }
        }

        // -------------------------------------------------------------------
        // MASZYNA STANÓW NAWIGACJI "WSTECZ" - TYLKO ZMIANA STANU + RE-RENDER
        // -------------------------------------------------------------------
        private void GoBackMenuLogic()
        {
            if (currentSubMenu == "Execute")
            {
                if (currentTab == "Audio")
                {
                    if (audioCategory == "Slaps") currentSubMenu = "Audio_Home";
                    else if (audioCategory == "Assets") currentSubMenu = "Audio_Assets";
                    else currentSubMenu = "Audio_PersonPicker";
                }
                else if (currentTab == "Breathing" || currentTab == "Penetration" || currentTab == "Gaze") currentSubMenu = "Select_Person";
                else { currentSubMenu = "Main"; currentTab = ""; selectedTargetName = ""; }
            }
            else if (currentSubMenu == "Audio_PersonPicker" || currentSubMenu == "Audio_Assets") currentSubMenu = "Audio_Home";
            else if (currentSubMenu == "Audio_Home" || currentSubMenu == "Select_Person") { currentSubMenu = "Main"; currentTab = ""; selectedTargetName = ""; audioCategory = ""; }

            CreateDynamicLayoutUI();
        }

        private void CreateScreenOverlayUI()
        {
            try
            {
                if (overlayCanvas != null) return;
                overlayCanvas = new GameObject("UltraScene_OverlayCanvas");
                Canvas canvas = overlayCanvas.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                overlayCanvas.AddComponent<CanvasScaler>();
                
                GameObject textObj = new GameObject("OverlayText"); textObj.transform.SetParent(overlayCanvas.transform, false);
                overlayTextInfo = textObj.AddComponent<Text>(); overlayTextInfo.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                overlayTextInfo.fontSize = 18; overlayTextInfo.color = new Color(0f, 0.75f, 0.9f, 0.9f); overlayTextInfo.alignment = TextAnchor.UpperRight;
                
                RectTransform rect = textObj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one; rect.anchoredPosition = new Vector2(-20, -20); rect.sizeDelta = new Vector2(400, 600);
                overlayCanvas.SetActive(true);
            }
            catch { }
        }

        private void SetBtnColor(UIDynamicButton element, Color c) { if (element == null) return; Text txt = element.GetComponentInChildren<Text>(); if (txt != null) txt.color = c; Image img = element.GetComponent<Image>(); if (img != null) img.color = new Color(0.06f, 0.06f, 0.08f); }

        // =====================================================================
        // AUDIO BANK: REALNY IMPORT CAŁEGO KATALOGU / WIELU PLIKÓW NA RAZ.
        // Wzorowany na potwierdzonym, działającym kodzie wtyczki dub.AudioMate
        // (AudioFileManager.cs) - używa wyłącznie publicznego, sandbox-bezpiecznego
        // API VaM, bez jakiegokolwiek odwołania do System.IO.
        // =====================================================================
        private class AudioBank
        {
            // Trwały zapis: lista UID klipów (= znormalizowanych ścieżek) rozdzielona "|".
            // "|" jest niedozwolony w nazwach plików Windows, więc jest bezpiecznym separatorem.
            private readonly JSONStorableString pathsStorable;
            public List<NamedAudioClip> Clips = new List<NamedAudioClip>();

            public AudioBank(JSONStorableString backingStorable)
            {
                pathsStorable = backingStorable;
            }

            // Wywoływane raz z Update(), gdy scena skończy się ładować - dopiero wtedy
            // VaM ma już przywrócone wartości storable'i zapisane w scenie.
            public void RebuildFromStorable()
            {
                Clips.Clear();
                if (pathsStorable == null || string.IsNullOrEmpty(pathsStorable.val)) return;
                string[] uids = pathsStorable.val.Split('|');
                foreach (string uid in uids)
                {
                    if (string.IsNullOrEmpty(uid)) continue;
                    NamedAudioClip clip = LoadClip(uid);
                    if (clip != null) Clips.Add(clip);
                }
            }

            public void ImportFolder(string folderPath)
            {
                string[] files = SuperController.singleton.GetFilesAtPath(folderPath);
                if (files != null)
                {
                    foreach (string f in files) AddIfAudio(f);
                }
                SyncStorable();
            }

            public void ImportFile(string path)
            {
                AddIfAudio(path);
                SyncStorable();
            }

            public void ClearAll()
            {
                Clips.Clear();
                SyncStorable();
            }

            // Losowy klip z banku - gotowy hak pod przyszłe wiązanie z Auto Orgasm / Auto Blowjob itd.
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
                foreach (NamedAudioClip c in Clips) uids.Add(c.uid);
                pathsStorable.val = string.Join("|", uids.ToArray());
            }
        }
    } // KLAMRA DEFINITYWNIE ZAMYKA KLASĘ GŁÓWNĄ UltraSceneController
} // KLAMRA DEFINITYWNIE ZAMYKA PRZESTRZEŃ NAZW namespace UltraScene

// =========================================================================
// INTEGRALNE GLOBALNE SZKIELETY KLAS PODMODUŁÓW WYKONAWCZYCH (ZGODNOŚĆ API VAM)
// =========================================================================
public class UltraAudioModule { public float volume, pitch; public bool is3DSound; public UltraAudioModule(MVRScript p, AudioSource s) {} public void PlayTargetSound(string c, Vector3 pos) {} public void NormalizeLoadedAudio() {} public void ClearAllFiles() {} }
public class UltraBreathingModule { public bool isEnabled; public float breathingSpeed, breathingIntense; public UltraBreathingModule(MVRScript p, AudioSource a) {} public void UpdateModule(Atom prs) {} }
public class UltraLickingModule { public bool isEnabled; public float lickingSpeed; public UltraLickingModule(MVRScript p) {} public void UpdateModule(Atom s, Atom t) {} }
public class UltraHandjobModule { public bool isEnabled; public float handjobSpeed, handjobIntense; public UltraHandjobModule(MVRScript p) {} public void UpdateModule(Atom s, Atom t) {} }
public class UltraBlowjobModule { public bool isEnabled; public float suckSpeed; public UltraBlowjobModule(MVRScript p) {} public void UpdateModule(Atom s, Atom t) {} }
public class UltraPenetrationModule { public bool playCollisionSounds; public float penetrationIntense; public UltraPenetrationModule(MVRScript p) {} public void UpdateModule(Atom s, Atom t, int idx) {} }
public class UltraExpressionModule { public float expressionSpeed, expressionIntense; public UltraExpressionModule(MVRScript p) {} public void UpdateModule(Atom s, Atom t, float exc) {} }
public class UltraRealismModule { public bool autoBreathing, autoLicking, autoForeskin, autoSucking, autoHandMovements, autoPenetrationSound, autoSlap, autoOrgasm, autoTessellation, autoFreezePose, autoMovement, autoNaturalMotion; public UltraRealismModule(MVRScript p) {} public void UpdateModule(List<Atom> l) {} public void ClearAllSpermManual() {} public void ResetAllJoints(List<Atom> l) {} public void TriggerPostOrgasmCalm(Atom a) {} public void ExportPluginSettings(object param) {} public void ImportPluginSettings(object param) {} }
public class UltraOrgasmSystem { public float currentExcitement; public bool isAutoOrgasmEnabled; public UltraOrgasmSystem(MVRScript p, UltraBlowjobModule b) {} public void UpdateSystem(Atom s, Atom t, string path) {} public void AddExcitement(float a) {} }
public class UltraGazeModule { public void UpdateModule(Atom s, Atom t) { } public UltraGazeModule(MVRScript p) { } }
