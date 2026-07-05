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

        // UZUPEŁNIONE POLA DLA NOWYCH MODUŁÓW AUDIO Z PLIKU PDF
        private UltraAmbientModule ambientModule;
        private UltraInteractionModule interactionModule;
        private UltraMotionModule motionModule;
        private UltraReactionModule reactionModule;
        private UltraStateSystem stateSystem;

        // MASZYNA STANÓW I PARAMETRY NAWIGACJI PROCEDURALNEGO INTERFEJSU
        private string currentTab = "";
        private string currentSubMenu = "Main"; 
        private string selectedTargetName = ""; 
        private GameObject overlayCanvas;
        private Text overlayTextInfo;

        // SYSTEM WYKRYWANIA I RESETOWANIA POZYCJI T (T-POSE RELEASE CORES)
        private List<string> relaxedPersons = new List<string>();

        // NOWE WSPÓŁDZIELONE ELEMENTY STRUKTURALNEGO HUD OSD (Z CLAUDE AI)
        private GameObject hudRoot;
        private List<GameObject> hudLineObjects = new List<GameObject>();
        private Font hudFont;
        
		// NOWE WSPÓŁDZIELONE POLA REŻYSERSKIEGO MANAGEMENTU FIZYKI (Z CLAUDE AI)
        private Dictionary<string, int> physicsChangeCounter = new Dictionary<string, int>();
        private Dictionary<string, float> physicsResetPending = new Dictionary<string, float>();
        private float physicsWatchTimer = 0f;
		
		// --- STRUKTURY OPTYMALIZACYJNE REŻYSERSKIEGO REALIZMU (PLAN JARLA PODŁEGO) ---
        private List<Atom> cachedPersons = new List<Atom>();
        private HashSet<string> hookedAtoms = new HashSet<string>();
        private int lastSceneSignature = 0;
        private int scanAtomCursor = 0;
        private Dictionary<string, float> lastResetTime = new Dictionary<string, float>();
        private ulong frameCounter = 0;

		
        // POLA ZAMRAŻACZA KRYOGENICZNEGO I DIAGNOSTYKI (Z CLAUDE AI)
        private bool lastFreezeState = false;
        private string lastLoggedError = null;
        private System.Collections.Generic.HashSet<string> unfreezeInProgress = new System.Collections.Generic.HashSet<string>();


        // SZTYWNA TABLICA NAZW PARAMETRÓW DO AUTOMATYCZNEGO RESETOWANIA BETONU W SUWAKACH VAM
        private readonly string[] controllerPhysicsParams = new string[]
        {
            "holdPositionSpring", "holdRotationSpring", "holdPositionDamper", "holdRotationDamper",
            "holdPositionMaxForce", "holdRotationMaxForce", "linkPositionSpring", "linkRotationSpring",
            "linkPositionDamper", "linkRotationDamper", "linkPositionMaxForce", "linkRotationMaxForce",
            "jointDriveSpring", "jointDriveDamper", "jointDriveMaxForce", "maxVelocity"
        };

        // =========================================================================
        // ULTRA-LEKKI MANAGED RESET POZY - ODPALA SIĘ DOKŁADNIE 1 RAZ NA PLIK (Z CLAUDE AI)
        // =========================================================================
        private System.Collections.IEnumerator ExecuteDelayedPhysicsReset(Atom atom)
        {
            if (atom == null) yield break;
            
            // Odczekujemy pół sekundy na pełne załadowanie pliku .pose do kości postaci
            yield return new WaitForSeconds(0.5f);
            
            // Chirurgiczne, jednorazowe przywrócenie suwaków do normy fabrycznej VaM
            ResetControllerPhysicsToDefaults(atom);
        }

        private void ResetControllerPhysicsToDefaults(Atom atom)
        {
            if (atom == null) return;
            
            try
            {
                FreeControllerV3[] fcs = atom.freeControllers;
                if (fcs == null) return;

                for (int c = 0; c < fcs.Length; c++)
                {
                    if (fcs[c] == null) continue;
                    for (int p = 0; p < controllerPhysicsParams.Length; p++)
                    {
                        JSONStorableFloat jsf = SafeGetFloat(fcs[c], controllerPhysicsParams[p]);
                        if (jsf != null)
                        {
                            jsf.val = jsf.defaultVal; // Powrót suwaka do fabrycznego standardu gry!
                        }
                    }
                }
                SuperController.LogMessage("[Ultra Physics] Pose Preset detected. Restored sliders to default VaM physics.");
            }
            catch (System.Exception)
            {
                // Ciche wygaszenie - system ponowi próbę w kolejnym cyklu
            }
        }

        private JSONStorableFloat SafeGetFloat(JSONStorable storable, string paramName)
        {
            if (storable == null) return null;
            try
            {
                return storable.GetFloatJSONParam(paramName);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        // =========================================================================
        // AMORTYZACJA UNFREEZE - MIĘKKIE WZNOWIENIE FIZYKI PRZECIW WYBUCHOM (Z CLAUDE AI)
        // =========================================================================
        private System.Collections.IEnumerator SoftUnfreeze(Atom atom)
        {
            if (atom == null) yield break;

            // 1. ANTY-EKSPLOZJA: Wyzerowanie prędkości ciał sztywnych postaci
            Rigidbody[] bodies = atom.gameObject.GetComponentsInChildren<Rigidbody>();
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] == null || bodies[i].isKinematic) continue;
                bodies[i].velocity = Vector3.zero;
                bodies[i].angularVelocity = Vector3.zero;
            }

            // 2. TŁUMIENIE: Tymczasowe zbicie sprężyn do 20% siły, aby zamortyzować start
            FreeControllerV3[] fcs = atom.freeControllers;
            Dictionary<JSONStorableFloat, float> savedSprings = new Dictionary<JSONStorableFloat, float>();
            string[] springParams = new string[] { "holdPositionSpring", "holdRotationSpring", "jointDriveSpring" };

            for (int c = 0; c < fcs.Length; c++)
            {
                if (fcs[c] == null) continue;
                for (int p = 0; p < springParams.Length; p++)
                {
                    JSONStorableFloat jsf = SafeGetFloat(fcs[c], springParams[p]);
                    if (jsf != null) 
                    { 
                        savedSprings[jsf] = jsf.val; 
                        jsf.val = jsf.val * 0.2f; 
                    }
                }
            }

            // --- OFICJALNE ODBLOKOWANIE CZASU W VaM (ZAPIS SETTEREM METODY SET) ---
            SuperController.singleton.SetFreezeAnimation(false);

            // 3. RAMPA: Płynny powrót sprężyn do 100% siły w czasie 0.4 sekundy
            float elapsed = 0f;
            while (elapsed < 0.4f)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / 0.4f);
                foreach (KeyValuePair<JSONStorableFloat, float> kv in savedSprings)
                {
                    if (kv.Key != null) kv.Key.val = Mathf.Lerp(kv.Value * 0.2f, kv.Value, progress);
                }
                yield return null;
            }

            // Ostateczne przywrócenie oryginalnych wartości suwaków
            foreach (KeyValuePair<JSONStorableFloat, float> kv in savedSprings)
            {
                if (kv.Key != null) kv.Key.val = kv.Value;
            }
        }

        // PANCERNE REJESTRY AKTUALIZACJI TEKSTÓW BEZ NISZCZENIA PRZYCISKÓW (Z CLAUDE AI)
        private bool hudDirty = true;
        private List<Text> hudLineLabels = new List<Text>();
        private List<System.Func<string>> hudLineTextGetters = new List<System.Func<string>>();
        private float hudPersonCheckTimer = 0f;
        private int lastPersonSignature = -1;


        // SŁOWNIKI OPERACYJNE STANU POSTACI (NIEZBĘDNE DLA SILNIKA UPDATE I INIT)
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
        private Dictionary<string, AudioBank> assetAudioBanks = new Dictionary<string, AudioBank>(); // Rejestruje tablicę banków dla prawego menu
        private AudioBank impactAudioBank;
        private bool audioBanksRestored = false;

        // KATEGORIE AUDIO PER POSTAĆ
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
        private const string DefaultAudioFolder = "Custom/Sounds/UltraSceneController";
        private List<Action> activeDynamicElements = new List<Action>();
        private string audioCategory = "";
        // POLA STANU NAWIGACJI AUDIO ZINTEGROWANE Z WZORCEM CLAUDE AI
        private List<UIDynamicButton> audioMenuButtons = new List<UIDynamicButton>();
        private string selectedAudioPersonUid = null;
        private string selectedAudioPersonName = null;

        // SŁOWNIK SSS DLA GLOBALNYCH PARAMETRÓW KONTROLEK INTERFEJSU
        public Dictionary<string, JSONStorableBool> pluginBools = new Dictionary<string, JSONStorableBool>();
        public Dictionary<string, JSONStorableFloat> pluginFloats = new Dictionary<string, JSONStorableFloat>();
        private Dictionary<string, JSONStorableStringChooser> personalityStorables = new Dictionary<string, JSONStorableStringChooser>();
        private Dictionary<string, JSONStorableStringChooser> moveStorables = new Dictionary<string, JSONStorableStringChooser>();
        private Dictionary<string, JSONStorableFloat> energyStorables = new Dictionary<string, JSONStorableFloat>();
        private Dictionary<string, JSONStorableStringChooser> assetCuaStorables = new Dictionary<string, JSONStorableStringChooser>();

        // JEDNORAZOWA REJESTRACJA PARAMETRÓW LOGICZNYCH W PAMIĘCI RAM
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

                    // OSTATECZNA SYNCHRONIZACJA LISTY 13 PROFILI OSOBOWOŚCI REŻYSERSKICH DLA POPRAWNEGO INTERFEJSU
                    var pChoice = new JSONStorableStringChooser("Person " + i + " Personality", 
                        new List<string> { 
                            "Neutral", 
                            "Sensual / Romantic", 
                            "Shy / Surprised", 
                            "Passionate / Ecstatic", 
                            "Angry / Surprised", 
                            "Fear / Scream", 
                            "Horny / Disgust", 
                            "Horny / Surprised", 
                            "Horny / Pain", 
                            "Evil / Horny", 
                            "Angry / Disgust", 
                            "Concerned / Surprised", 
                            "Great / Surprised" 
                        }, 
                        "Neutral", "Personality", (string val) => { if(!string.IsNullOrEmpty(val)) personPersonality[key] = val; });
                    RegisterStringChooser(pChoice); 
                    personalityStorables[key] = pChoice;
                    var mChoice = new JSONStorableStringChooser("Person " + i + " Move Target", 
                        new List<string> { "None", "Camera", "Person 1", "Person 2", "Person 3", "Person 4" }, 
                        "None", "Move Target", (string val) => { if(!string.IsNullOrEmpty(val)) personMoveTarget[key] = val; });
                    RegisterStringChooser(mChoice); moveStorables[key] = mChoice;

                    var aSlider = new JSONStorableFloat("Person " + i + " Energy Level", 0f, (float val) => { 
                        personEnergyLevel[key] = val; 
                    }, 0f, 100f, true);
                    RegisterFloat(aSlider); energyStorables[key] = aSlider;

                    var audioPathsStorable = new JSONStorableString("Person " + i + " Audio Files", "");
                    RegisterString(audioPathsStorable);
                    personAudioBanks[key] = new AudioBank(audioPathsStorable);
                }

                // INICJALIZACJA 10 ROZWIJANYCH LIST DLA IDENTYFIKACJI OBIEKTÓW CUA W GRZE
                for (int a = 1; a <= 10; a++)
                {
                    string assetKey = "Asset " + a;
                    var cuaChooser = new JSONStorableStringChooser(assetKey + " Target Object", new List<string> { "None" }, "None", "CUA Object Target");
                    RegisterStringChooser(cuaChooser);
                    assetCuaStorables[assetKey] = cuaChooser;

                    var assetPathsStorable = new JSONStorableString(assetKey + " Audio Files", "");
                    RegisterString(assetPathsStorable);
                    assetAudioBanks[assetKey] = new AudioBank(assetPathsStorable);
                }

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
                    "RLAutoBreathing", "RLAutoBlinking", "RLAutoClothing", "RLAutoLipSync", "RLAutoHandMovements", 
                    "RLAutoEnvironmentSound", "RLAutoPhysics", "RLAutoTransitions", "RLAutoTessellation",
                    "RLAutoFreezePose", "RLAutoMovement", "RLAutoNaturalMotion" 
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
                UpdateSceneCacheAndHooks();
            }
            catch (Exception e) 
            { 
                SuperController.LogError("Init Error: " + e.Message); 
            }
        }

        private void InitStorableParameters()
		
		// --- POMOCNIK WARSTWY 2: ZLICZANIE ODCHYLEŃ FIZYKI OD DEFAULTOW SILNIKA VaM ---
        private int CountControllerDeviations(Atom atom)
		
		// --- WARSTWA 1C: REJESTRACJA CALL-BACKU DETEKCJI PRESETÓW POZY (Z CLAUDE AI / PODŁY) ---
        
		// --- WARSTWA 1B: ZARZĄDZANIE CACHEM POSTACI I REJESTRACJĄ HOOKÓW (PLAN JARLA PODŁEGO) ---
 
        // --- WARSTWA 2: REALIZACJA LAZY FALLBACK (SKANOWANIE JEDNEGO ATOMU NA CYKL) ---
        private void ExecuteLazyFallbackScan()
        {
            int personCount = cachedPersons.Count;
            if (personCount == 0) return;

            Atom targetAtom = cachedPersons[scanAtomCursor % personCount];
            scanAtomCursor++; 

            if (targetAtom == null || !targetAtom.on) return;

            string uid = targetAtom.uid;
            float currentTime = Time.time;

            if (physicsResetPending.ContainsKey(uid)) return;
            if (lastResetTime.ContainsKey(uid) && (currentTime - lastResetTime[uid]) < 2.0f) return;

            int deviations = CountControllerDeviations(targetAtom); 

            int prev = 0;
            physicsChangeCounter.TryGetValue(uid, out prev);

            if (deviations - prev >= 8) 
            {
                physicsResetPending[uid] = currentTime + 0.5f; 
            }
            physicsChangeCounter[uid] = deviations;
        }

        // --- WARSTWA 3: WYKONANIE KOLEJKI RESETÓW (MAKSYMALNIE JEDEN RESET NA KLATKĘ) ---
        private void ProcessSinglePhysicsReset()
        {
            if (physicsResetPending.Count == 0) return; 

            string readyUid = null;
            float currentTime = Time.time;
            
            foreach (KeyValuePair<string, float> kv in physicsResetPending)
            {
                if (currentTime >= kv.Value) 
                { 
                    readyUid = kv.Key; 
                    break; 
                }
            }
            if (readyUid == null) return;

            physicsResetPending.Remove(readyUid);
            Atom target = SuperController.singleton.GetAtomByUid(readyUid);
            
            if (target != null && target.on)
            {
                ResetControllerPhysicsToDefaults(target); 
                physicsChangeCounter[readyUid] = 0;
                lastResetTime[readyUid] = currentTime;    
            }
        }
 
		// --- POMOCNIK WARSTWY 1B: LICZENIE SPÓJNEJ SYGNATURY SKŁADU SCENY ---
        private int ComputePersonSignature()
        {
            int signature = 0;
            List<Atom> allAtoms = SuperController.singleton.GetAtoms();
            if (allAtoms == null) return 0;

            for (int i = 0; i < allAtoms.Count; i++)
            {
                Atom atom = allAtoms[i];
                // Identyczny warunek jak w cache: tylko włączone postacie (brak duchów!)
                if (atom != null && atom.type == "Person" && atom.on)
                {
                    if (atom.uid != null)
                    {
                        signature += atom.uid.GetHashCode();
                    }
                }
            }
            return signature;
        }		
		private void UpdateSceneCacheAndHooks()
        {
            int currentSignature = ComputePersonSignature();
            if (currentSignature == lastSceneSignature) return;

            lastSceneSignature = currentSignature;
            cachedPersons.Clear();

            List<Atom> allAtoms = SuperController.singleton.GetAtoms();
            HashSet<string> currentUids = new HashSet<string>();

            for (int i = 0; i < allAtoms.Count; i++)
            {
                Atom atom = allAtoms[i];
                if (atom != null && atom.type == "Person" && atom.on)
                {
                    cachedPersons.Add(atom);
                    currentUids.Add(atom.uid);

                    if (!hookedAtoms.Contains(atom.uid))
                    {
                        HookPoseLoadDetection(atom);
                        hookedAtoms.Add(atom.uid);
                    }
                }
            }

            // Bezpiecznik Podłego: Usuwamy UID z pamięci TYLKO, gdy postać przestała istnieć w VaM
            List<string> toRemove = null;
            foreach (string uid in hookedAtoms)
            {
                if (SuperController.singleton.GetAtomByUid(uid) == null)
                {
                    if (toRemove == null) toRemove = new List<string>();
                    toRemove.Add(uid);
                }
            }
            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++) 
                {
                    hookedAtoms.Remove(toRemove[i]);
                }
            }
        }
		
		private void HookPoseLoadDetection(Atom atom)
        {
            if (atom == null) return;
            JSONStorable posePresets = atom.GetStorableByID("PosePresets");
            if (posePresets == null) return;

            JSONStorableStringChooser presetChooser = posePresets.GetStringChooserJSONParam("presetName");
            if (presetChooser == null) return;

            string capturedUid = atom.uid; 
            JSONStorableStringChooser.SetStringCallback oldCallback = presetChooser.setCallbackFunction;

            presetChooser.setCallbackFunction = delegate (string newVal)
            {
                if (oldCallback != null) oldCallback(newVal); 
                physicsResetPending[capturedUid] = Time.time + 0.75f; 
            };
        }

		
        {
            if (atom == null) return 0;
            FreeControllerV3[] controllers = atom.freeControllers;
            if (controllers == null) return 0;

            int deviations = 0;
            for (int c = 0; c < controllers.Length; c++)
            {
                FreeControllerV3 fc = controllers[c];
                if (fc == null) continue;

                for (int p = 0; p < controllerPhysicsParams.Length; p++)
                {
                    JSONStorableFloat jsf = fc.GetFloatJSONParam(controllerPhysicsParams[p]);
                    if (jsf != null && Mathf.Abs(jsf.val - jsf.defaultVal) > 0.01f)
                    {
                        deviations++;
                    }
                }
            }
            return deviations;
        }

		
        {
            RegisterBool(new JSONStorableBool("Master Audio Switch", true));
            RegisterBool(new JSONStorableBool("Master Simulation Switch", true));
            RegisterFloat(new JSONStorableFloat("Global Output Gain", 0.8f, 0f, 1f));
        }
		
        // =========================================================================
        // ROZWIĄZANIE PODŁEGO: PANCERNE WYSZUKIWANIE I ROZLUŹNIANIE KONTROLERÓW
        // =========================================================================
        private FreeControllerV3 FindController(Atom atom, string controllerName)
        {
            if (atom == null || atom.freeControllers == null) return null;

            FreeControllerV3[] fcs = atom.freeControllers;
            for (int i = 0; i < fcs.Length; i++)
            {
                if (fcs[i] != null && fcs[i].name == controllerName)
                {
                    return fcs[i];
                }
            }
            return null;
        }

        private void RelaxHands(Atom atom)
        {
            if (atom == null) return;

            string[] handIds = new string[] { "lHandControl", "rHandControl", "lArmControl", "rArmControl" };
            for (int i = 0; i < handIds.Length; i++)
            {
                FreeControllerV3 fc = FindController(atom, handIds[i]);
                if (fc == null) continue;
                fc.currentPositionState = FreeControllerV3.PositionState.Off;
                fc.currentRotationState = FreeControllerV3.RotationState.Off;
            }
        }
		
        // =========================================================================
        // NOWA DYNAMICZNA FABRYKA KAFELKÓW OSD Z ODŚWIEŻANIEM TEKSTU (Z CLAUDE AI)
        // =========================================================================
        private void AddHudLine(System.Func<string> textGetter, System.Action onClick)
        {
            if (hudRoot == null) return;

            GameObject lineObj = new GameObject("HudLine");
            lineObj.transform.SetParent(hudRoot.transform, false);

            // Tło wiersza - tworzymy czystą, białą teksturę jako bazę dla maskowania kolorów przycisku Unity
            Image bg = lineObj.AddComponent<Image>();
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            bg.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));

            // Tekst jako niezależny element nadrzędny (dziecko)
            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(lineObj.transform, false);
            RectTransform trt = textObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12f, 2f); // Padding wewnętrzny
            trt.offsetMax = new Vector2(-12f, -2f);

            Text label = textObj.AddComponent<Text>();
            label.font = hudFont;
            label.fontSize = 15; // Lekko zmniejszona czcionka pod ściśnięte menu
            label.color = Color.white;
            label.supportRichText = true; 
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.alignment = TextAnchor.MiddleLeft;
            
            label.text = textGetter != null ? textGetter() : "";
            label.raycastTarget = false; // Mysz ignoruje litery, uderza w fizyczny prostokąt tła

            // Wymiarowanie wiersza pod automatyczny pionowy layout Unity
            LayoutElement le = lineObj.AddComponent<LayoutElement>();
            le.preferredHeight = 22f; // WYTYCZNA: Ścinamy wysokość wiersza z 26f na 22f dla pancernego ściśnięcia!

            if (onClick != null)
            {
                Button btn = lineObj.AddComponent<Button>();
                btn.targetGraphic = bg;

                // OSTATECZNA KOREKTA HOVERA: normalColor dziedziczy przezroczysty grafit, pozwalając mu żyć!
                ColorBlock cb = btn.colors;
                cb.normalColor = new Color(0.12f, 0.12f, 0.14f, 0.55f); // Piękne, lekko przezroczyste grafitowe tło wiersza!
                cb.highlightedColor = new Color(0.3137f, 0.3137f, 0.3137f, 1f); // Nasz hollywoodzki Hover #505050!
                cb.pressedColor = new Color(0.18f, 0.18f, 0.18f, 1f);
                cb.colorMultiplier = 1f;
                btn.colors = cb;

                System.Action captured = onClick; // Kopia dla Mono
                btn.onClick.AddListener(delegate () { captured(); });
            }
            else
            {
                // Linie informacyjne (np. nagłówki, imiona postaci) mają surowe, ciemniejsze tło i nie reagują na mysz
                bg.color = new Color(0.04f, 0.04f, 0.05f, 0.85f);
                bg.raycastTarget = false; 
            }

            hudLineLabels.Add(label);
            hudLineTextGetters.Add(textGetter);
            hudLineObjects.Add(lineObj);
        }

        // =========================================================================
        // MANAGED SYSTEM AKTUALIZACJI STRUKTURY I FILTROWANIA TEKSTÓW (Z CLAUDE AI)
        // =========================================================================
        private void UpdateHudSystem()
        {
            // 1. Raz na sekundę sprawdź, czy zmienił się skład postaci na scenie
            hudPersonCheckTimer += Time.deltaTime;
            if (hudPersonCheckTimer >= 1f)
            {
                hudPersonCheckTimer = 0f;
                int signature = ComputePersonSignature();
                if (signature != lastPersonSignature)
                {
                    lastPersonSignature = signature;
                    hudDirty = true;
                }
            }

            // 2. Pełna przebudowa klocków UI tylko gdy flaga "dirty" jest aktywna
            if (hudDirty)
            {
                hudDirty = false;
                RebuildHUD();
                return;
            }

            // 3. Tania aktualizacja: podmieniamy same stringi w istniejących labelkach bez niszczenia obiektów!
            for (int i = 0; i < hudLineLabels.Count; i++)
            {
                if (hudLineLabels[i] == null || hudLineTextGetters[i] == null) continue;
                string fresh = hudLineTextGetters[i]();
                if (hudLineLabels[i].text != fresh) hudLineLabels[i].text = fresh;
            }
        }

        private int ComputePersonSignature()
        {
            int sig = 17;
            List<Atom> atoms = SuperController.singleton.GetAtoms();
            for (int i = 0; i < atoms.Count; i++)
            {
                Atom a = atoms[i];
                if (a == null || a.type != "Person" || !a.on) continue;
                sig = sig * 31 + a.uid.GetHashCode();
            }
            return sig;
        }

        private void RebuildHUD()
        {
            if (hudLineObjects == null) return;

            for (int i = 0; i < hudLineObjects.Count; i++)
            {
                if (hudLineObjects[i] != null) hudLineObjects[i].SetActive(false);
            }

            int currentLineIdx = 0;

            // Pancerne narzędzie pomocnicze do wstrzykiwania stanów w istniejącą sieć kafelków
            Action<System.Func<string>, System.Action, bool> SetupLine = (textGetter, onClick, isHeader) => {
                if (currentLineIdx >= hudLineObjects.Count) return;

                GameObject lineObj = hudLineObjects[currentLineIdx];
                Text label = hudLineLabels[currentLineIdx];
                hudLineTextGetters[currentLineIdx] = textGetter;

                label.text = textGetter != null ? textGetter() : "";
                
                Image bg = lineObj.GetComponent<Image>();
                Button btn = lineObj.GetComponent<Button>();

                if (isHeader)
                {
                    // Nagłówki i paski sekcji: brak interakcji, brak Hovera, surowe przezroczyste tło
                    if (bg != null) { bg.color = new Color(0f, 0f, 0f, 0.55f); bg.raycastTarget = false; }
                    if (btn != null) btn.enabled = false;
                    label.alignment = TextAnchor.MiddleCenter; // Centrowanie myślników
                }
                else if (onClick != null)
                {
                    // Zwykłe, aktywne przyciski: aktywujemy skrypt Hovera, pozwalamy mu sterować tłem!
                    if (bg != null) { bg.raycastTarget = true; }
                    
                    // Przywracamy domyślne kolory spoczynkowe skryptu, aby wyczyścić stany po poprzednich klatkach
                    ButtonTextHover hvr = lineObj.GetComponent<ButtonTextHover>();
                    if (hvr != null) { bg.color = hvr.normalBgColor; label.color = hvr.normalColor; }

                    if (btn != null)
                    {
                        btn.enabled = true;
                        btn.onClick.RemoveAllListeners();
                        System.Action captured = onClick;
                        btn.onClick.AddListener(delegate () { captured(); });
                    }
                    label.alignment = TextAnchor.MiddleLeft;
                }

                else
                {
                    // Linie czysto informacyjne (np. Arousal lub imiona postaci)
                    if (bg != null) { bg.color = new Color(0f, 0f, 0f, 0.35f); bg.raycastTarget = false; }
                    if (btn != null) btn.enabled = false;
                    label.alignment = TextAnchor.MiddleLeft;
                }

                lineObj.SetActive(true);
                currentLineIdx++;
            };

            // 1. TYTUŁ GŁÓWNY WTYCZKI - PRZYWRÓCONY W 100% Z TWÓJ PLIK Z MYŚLNIKAMI I KOLOREM NIEBIESKIM
            SetupLine(
                delegate () { return "<color=#2255ee><b>------------------Ultra Scene Controller------------------\n</b></color>"; }, 
                null, 
                true // Traktuj jako nagłówek
            );

            List<Atom> allAtoms = SuperController.singleton.GetAtoms();
            int activeIndex = 1;

            // 2. DYNAMICZNA OBSŁUGA POSTACI (MAKSYMALNIE 4)
            for (int i = 0; i < allAtoms.Count; i++)
            {
                Atom a = allAtoms[i];
                if (a == null || a.type != "Person" || !a.on || activeIndex > 4) continue;

                string key = "Person " + activeIndex;
                string realName = !string.IsNullOrEmpty(a.name) ? a.name : a.uid;
                string capturedKey = key;

                SetupLine(delegate () { return "<color=#f165f1><b>" + realName + "</b></color>"; }, null, false);

                JSONStorableStringChooser personalityChooser = personalityStorables.ContainsKey(capturedKey) ? personalityStorables[capturedKey] : null;
                var localPers = personalityChooser;
                SetupLine(
                    delegate () { return " Personality: <color=#00e5ff>[" + (localPers != null ? localPers.val : "Neutral") + "]</color>"; },
                    delegate () { CycleChooser(localPers); },
                    false
                );

                SetupLine(
                    delegate () {
                        float energy = 0f; 
                        if (energyStorables.ContainsKey(capturedKey) && energyStorables[capturedKey] != null) energy = energyStorables[capturedKey].val;
                        return " Arousal: <color=#ffea00>[" + energy.ToString("F0") + "%]</color>";
                    }, 
                    null, false
                );

                JSONStorableStringChooser moveChooser = moveStorables.ContainsKey(capturedKey) ? moveStorables[capturedKey] : null;
                var localMove = moveChooser;
                SetupLine(
                    delegate () { return " Move: <color=#00ff66>[" + (localMove != null ? localMove.val : "None") + "]</color>"; },
                    delegate () { CycleChooser(localMove); },
                    false
                );

                activeIndex++;
            }

            // 3. SEKCJA REALIZMU - PRZYWRÓCONA W 100% Z TWÓJ PLIK Z MYŚLNIKAMI I KOLOREM CZERWONYM
            SetupLine(
                delegate () { return "<color=#d62a2e><b>----------------------------Realism----------------------------\n</b></color>"; }, 
                null, 
                true // Traktuj jako nagłówek
            );

            string[] displayRealismNames = { 
                "Auto Breathing system", "Auto Licking system", "Auto Foreskin system", "Auto Sucking system", 
                "Auto Hand Movements system", "Auto Penetration Sound system", "Auto Slap system", "Auto Orgasm system", 
                "Auto Normalize Audio system", "Auto Tessellation system", "Auto Dynamic Skin Wetness system", 
                "Auto Reset Joints system", "Auto Freeze Pose system", "Auto Movement system", "Auto Natural Motion system", 
                "Auto Micro Muscle Drift system" 
            };

            string[] realismKeys = { 
                "RLAutoBreathing", "RLAutoLicking", "RLAutoForeskin", "RLAutoSucking", 
                "RLAutoHandMovements", "RLAutoPenetrationSound", "RLAutoSlap", "RLAutoOrgasm", 
                "RLAutoNormalizeAudio", "RLAutoTessellation", "RLAutoDynamicSkinWetness", 
                "RLAutoResetJoints", "RLAutoFreezePose", "RLAutoMovement", "RLAutoNaturalMotion", 
                "RLAutoMicroMuscleDrift" 
            };

            for (int r = 0; r < realismKeys.Length; r++)
            {
                string rKey = realismKeys[r];
                string dName = displayRealismNames[r];
                JSONStorableBool toggle = GetBool(rKey);
                var localToggle = toggle;
                
                SetupLine(
                    delegate () {
                        bool state = (localToggle != null && localToggle.val);
                        string statusLabel = state ? "<color=#00ff00><b>[ON]</b></color>" : "<color=#888888>[OFF]</color>";
                        return " " + dName + ": " + statusLabel;
                    }, 
                    delegate () {
                        if (localToggle != null) { localToggle.val = !localToggle.val; CreateDynamicLayoutUI(); }
                    },
                    false
                );
            }
        }

        private void CycleChooser(JSONStorableStringChooser storable)
        {
            if (storable == null || storable.choices == null || storable.choices.Count == 0) return;
            int idx = storable.choices.IndexOf(storable.val);
            storable.val = storable.choices[(idx + 1) % storable.choices.Count];
            // WYTYCZNA: Usunięto wywołanie RebuildHUD()! Wartości odświeżą się same, zostawiamy tylko panel UI
            CreateDynamicLayoutUI(); 
        }

        // === PRYWATNA FLAGA DO ZABEZPIECZENIA JEDNORAZOWEGO SPIĘCIA PRZYCISKU ===
        private bool poseButtonHooked = false;

    // ====================================================================================
    // SCALONA I ZOPTYMALIZOWANA PĘTLA WYKONAWCZA (WARSTWY 1, 2, 3 + INTEGRACJA MODUŁÓW)
    // ====================================================================================
    public void Update()
    {
        try
        {
            if (SuperController.singleton == null) return;
            frameCounter++;

            // Warstwa 1A: Zarządzanie Cache i Rejestracja Hooków (Co 60 klatek, offset 0)
            if (frameCounter % 60 == 0)
            {
                UpdateSceneCacheAndHooks();
            }

            // Warstwa 2: Leniwy Fallback Skanu Odchyleń (Co 150 klatek ~ 2.5s przy 60fps, offset 20)
            if (frameCounter % 150 == 20)
            {
                ExecuteLazyFallbackScan();
            }

            // Warstwa 3: Wykonanie Kolejki Resetów - MAX 1 na klatkę (Ultra lekkie, co klatkę)
            ProcessSinglePhysicsReset();

            // --- PROCEDURY INICJALNE AUDIO ---
            if (!audioBanksRestored && !SuperController.singleton.isLoading)
            {
                audioBanksRestored = true;
                if (personAudioBanks != null)
                {
                    List<string> personKeys = new List<string>(personAudioBanks.Keys);
                    for (int i = 0; i < personKeys.Count; i++) { AudioBank bank = personAudioBanks[personKeys[i]]; if (bank != null) bank.RebuildFromStorable(); }
                }
                if (assetAudioBanks != null)
                {
                    List<string> assetKeys = new List<string>(assetAudioBanks.Keys);
                    for (int i = 0; i < assetKeys.Count; i++) { AudioBank bank = assetAudioBanks[assetKeys[i]]; if (bank != null) bank.RebuildFromStorable(); }
                }
                if (impactAudioBank != null) impactAudioBank.RebuildFromStorable();
                if (simpleAudioBanks != null)
                {
                    List<string> simpleKeys = new List<string>(simpleAudioBanks.Keys);
                    for (int i = 0; i < simpleKeys.Count; i++) { AudioBank bank = simpleAudioBanks[simpleKeys[i]]; if (bank != null) bank.RebuildFromStorable(); }
                }
            }

            // --- INTEGRACJA PODMODUŁÓW I LOGIKI POSTACI Z OPTYMALNEGO CACHE ---
            int pCount = 1;
            for (int i = 0; i < cachedPersons.Count; i++)
            {
                Atom atom = cachedPersons[i];
                if (atom == null || !atom.on) continue; // Null-check na wypadek usunięcia postaci w locie

                string relaxationKey = atom.uid; 
                string key = "Person " + pCount;

                // INICJALNE JEDNORAZOWE UWOLNIENIE Z POZYCJI T-POSE
                if (!relaxedPersons.Contains(relaxationKey) && pCount <= 4)
                {
                    JSONStorable physicsStorable = atom.GetStorableByID("Physics");
                    if (physicsStorable != null) physicsStorable.CallAction("Simulate");
                    
                    string[] targets = { "hipControl", "chestControl", "headControl", "leftHandControl", "rightHandControl", "leftFootControl", "rightFootControl" };
                    for (int t = 0; t < targets.Length; t++)
                    {
                        JSONStorable ctrl = atom.GetStorableByID(targets[t]);
                        if (ctrl != null) ctrl.CallAction("ON");
                    }
                    
                    relaxedPersons.Add(relaxationKey);
                }

                if (pCount <= 4)
                {
                    personKeyByAtomUid[atom.uid] = key;
                    
                    if (breathingModule != null) breathingModule.UpdateModule(atom);
                    if (gazeModule != null) gazeModule.UpdateModule(atom, cachedPersons); // Karmimy moduł tanim cache'em!
                }

                // --- DETEKTOR ZBOCZA ZAMRAŻANIA ANIMACJI VaM ---
                bool currentGlobalFreeze = SuperController.singleton.freezeAnimation;
                if (lastFreezeState && !currentGlobalFreeze)
                {
                    if (!unfreezeInProgress.Contains(atom.uid))
                    {
                        StartCoroutine(ExecuteSafeUnfreeze(atom));
                    }
                }
                lastFreezeState = currentGlobalFreeze;

                // --- SIMULATOR NATURALNEGO RUCHU (SZUM PERLINA) ---
                JSONStorableBool freezeCheck = GetBool("RLAutoFreezePose");
                bool isPoseFrozen = (freezeCheck != null && freezeCheck.val) || currentGlobalFreeze;
                if (!isPoseFrozen && pCount <= 4)
                {
                    JSONStorableBool naturalMotionCheck = GetBool("RLAutoNaturalMotion");
                    JSONStorableBool muscleDriftCheck = GetBool("RLAutoMicroMuscleDrift");
                    float seedBase = (float)pCount * 12.34f; 
                     float timeFactor = Time.time;
                    Vector3 perlinOffset = Vector3.zero;

                    if (naturalMotionCheck != null && naturalMotionCheck.val)
                    {
                        float nX = (Mathf.PerlinNoise(timeFactor * 0.25f, seedBase) - 0.5f) * 0.035f; 
                        float nY = (Mathf.PerlinNoise(timeFactor * 0.15f, seedBase + 5f) - 0.5f) * 0.015f;
                        float nZ = (Mathf.PerlinNoise(timeFactor * 0.20f, seedBase + 10f) - 0.5f) * 0.025f;
                        perlinOffset += new Vector3(nX, nY, nZ);
                    }
                    if (muscleDriftCheck != null && muscleDriftCheck.val)
                    {
                        float mX = (Mathf.PerlinNoise(timeFactor * 2.5f, seedBase + 20f) - 0.5f) * 0.004f;
                        float mY = (Mathf.PerlinNoise(timeFactor * 3.0f, seedBase + 25f) - 0.5f) * 0.003f;
                        float mZ = (Mathf.PerlinNoise(timeFactor * 2.1f, seedBase + 30f) - 0.5f) * 0.004f;
                        perlinOffset += new Vector3(mX, mY, mZ);
                    }
                    if (perlinOffset != Vector3.zero)
                    {
                        atom.transform.position += atom.transform.TransformDirection(perlinOffset * Time.deltaTime * 60f);
                    }
                }

                // --- BLOKADA NATYWNYCH AUTOMATYZMÓW VaM ---
                JSONStorable autoExpressionsStorable = atom.GetStorableByID("AutoExpressions");
                if (autoExpressionsStorable != null)
                {
                    JSONStorableBool autoExprEnabled = autoExpressionsStorable.GetBoolJSONParam("enabled");
                    if (autoExprEnabled != null && autoExprEnabled.val) autoExprEnabled.val = false;
                    JSONStorableBool blinkToggleAE = autoExpressionsStorable.GetBoolJSONParam("blinkEnabled");
                    if (blinkToggleAE != null && blinkToggleAE.val) blinkToggleAE.val = false;
                }
                JSONStorable eyelidControlStorable = atom.GetStorableByID("EyelidControl");
                if (eyelidControlStorable != null)
                {
                    JSONStorableBool blinkToggle = eyelidControlStorable.GetBoolJSONParam("blinkEnabled");
                    if (blinkToggle != null && blinkToggle.val) blinkToggle.val = false;
                }

                // MIMIKA I EKSPRESJA TWARZY (MODUŁ WYKONAWCZY)
                if (expressionModule != null && pCount <= 4)
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
                pCount++;
            }

            // DIAGNOSTYCZNA OSŁONA SYSTEMU HUD
            try { UpdateHudSystem(); } catch (System.Exception ex) { SuperController.LogError("[HUD Subsystem Error] " + ex.Message + "\n" + ex.StackTrace); }

            // AUTOMATYKA INTERAKCJI ZBLIŻENIOWEJ CO KLATKĘ
            JSONStorableBool automaticToggle = GetBool("IKEnable");
            if (automaticToggle != null && automaticToggle.val)
            {
                UpdateActionProximitySystem(cachedPersons); // Podajemy lekki cache zamiast ciężkiego GetAtoms!
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
        catch (System.Exception ex)
        {
            string sig = ex.Message + (ex.StackTrace != null ? ex.StackTrace : "");
            if (sig != lastLoggedError)
            {
                lastLoggedError = sig;
                SuperController.LogError("[UltraScene Core Critical Error] " + ex.Message + "\n" + ex.StackTrace);
            }
        }
    }

                // DIAGNOSTYCZNA OSŁONA SYSTEMU HUD (WYTYCZNA PODŁEGO)
                try { UpdateHudSystem(); } catch (System.Exception ex) { SuperController.LogError("[HUD Subsystem Error] " + ex.Message + "\n" + ex.StackTrace); }

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
            catch (System.Exception ex)
            {
                // DIAGNOSTYCZNY MANAGED CATCH OD CLAUDE AI PRZECIW ZALEWANIU LOGÓW
                string sig = ex.Message + (ex.StackTrace != null ? ex.StackTrace : "");
                if (sig != lastLoggedError)
                {
                    lastLoggedError = sig;
                    SuperController.LogError("[UltraScene Core Critical Error] " + ex.Message + "\n" + ex.StackTrace);
                }
            }
        }

        // METODA WRAPPER Z PASAMI I SZELKAMI DLA UNIKNIĘCIA PODWÓJNYCH COROUTINE (Z CLAUDE AI)
        private System.Collections.IEnumerator ExecuteSafeUnfreeze(Atom atom)
        {
            if (atom == null) yield break;
            unfreezeInProgress.Add(atom.uid);
            yield return StartCoroutine(SoftUnfreeze(atom));
            unfreezeInProgress.Remove(atom.uid);
        }

        public void OnDestroy() 
        { 
            if (overlayCanvas != null) UnityEngine.Object.Destroy(overlayCanvas); 
        }
        // =========================================================================
        // SYSTEM AKCJI ZBLIŻENIOWEJ: Bliskość startuje sekwencję, Velocity ustala tempo.
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
        // MASZYNA INTERFEJSU: Dynamiczne renderowanie drzewa kafelków i nawigacji
        // =========================================================================
        private void CreateDynamicLayoutUI()
        {
            try
            {
                ClearDynamicLayout();
                
                // Generowanie przycisków powrotu na samej górze paneli dynamicznych
                if (currentSubMenu != "Main" && currentSubMenu != "Audio_SubCategoryPicker") 
                {
                    AddBackButton();
                }

                switch (currentSubMenu)
                {
                    case "Audio_Home": RenderAudioHome(); break;
                    case "Audio_SubCategoryPicker": RenderAudioSubCategoryPicker(); break;
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
            Color backGold, menuBlue;
            ColorUtility.TryParseHtmlString("#ffeb3b", out backGold); // WYTYCZNA: Kolor tekstu dla Back to Previous powraca na żółty #ffeb3b
            ColorUtility.TryParseHtmlString("#2255ee", out menuBlue); // Oryginalny niebieski dla Main Menu
            
            AddButton("Back to Previous", false, backGold, () => { GoBackMenuLogic(); });
            AddButton("Go to Main Menu", true, menuBlue, () => { 
                currentSubMenu = "Main"; 
                currentTab = ""; 
                selectedTargetName = ""; 
                audioCategory = ""; 
                CreateDynamicLayoutUI(); 
            });
        }

        // UNIWERSALNA FABRYKA DYNAMICZNYCH PRZYCISKÓW GRY VaM
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
            Color realismRed = new Color(0.839f, 0.165f, 0.180f); // Czerwony #d62a2e
            string[] technicalTabs = { "Audio", "Handjob", "Breathing", "Blowjob", "Penetration", "Expression", "Gaze", "Realism" };
            string[] displayLabels = { "Audio", "Handjob", "Breathing", "Blowjob", "Penetration", "Gaze & Glance", "Expression", "Realism" };
            
            for (int i = 0; i < technicalTabs.Length; i++)
            {
                string targetTab = technicalTabs[i];
                bool isRight = (i % 2 != 0);
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

        private void ClearAudioMenuButtons()
        {
            for (int i = 0; i < audioMenuButtons.Count; i++)
            {
                if (audioMenuButtons[i] != null) try { RemoveButton(audioMenuButtons[i]); } catch {}
            }
            audioMenuButtons.Clear();
        }

        // PANEL 1: "Audio_Home" - Dynamiczna lista postaci + uniwersalny "Slaps" oraz 10 rzymskich kafelków CUA
        private void RenderAudioHome()
        {
            ClearAudioMenuButtons();
            Color personPink, whiteTxt, assetGreen;
            ColorUtility.TryParseHtmlString("#f165f1", out personPink);
            ColorUtility.TryParseHtmlString("#f5f5f5", out whiteTxt); // WYTYCZNA: Zmiana koloru czcionki Slaps na matową biel #f5f5f5
            ColorUtility.TryParseHtmlString("#00c800", out assetGreen); // Klasyczna retro zieleń
            
            List<Atom> allAtoms = SuperController.singleton.GetAtoms();
            int personCount = 0;
            for (int i = 0; i < allAtoms.Count; i++)
            {
                Atom candidate = allAtoms[i];
                if (candidate == null || candidate.type != "Person" || !candidate.on) continue;
                string personKey;
                if (!personKeyByAtomUid.TryGetValue(candidate.uid, out personKey)) continue;
                string capturedUid = candidate.uid;
                string capturedKey = personKey; 

                UIDynamicButton personBtn = AddButton(capturedUid, false, personPink, () =>
                {
                    selectedAudioPersonUid = capturedUid;
                    selectedAudioPersonName = capturedKey; 
                    currentSubMenu = "Audio_SubCategoryPicker"; 
                    CreateDynamicLayoutUI();
                });
                if (personBtn != null) audioMenuButtons.Add(personBtn);
                personCount++;
            }
            if (personCount == 0)
            {
                Color errorRed; ColorUtility.TryParseHtmlString("#ff0000", out errorRed);
                UIDynamicButton emptyInfo = AddButton("No Person atoms detected", false, errorRed, null, false);
                if (emptyInfo != null) audioMenuButtons.Add(emptyInfo);
            }

            UIDynamicButton slapsBtn = AddButton("Slaps", false, whiteTxt, () =>
            {
                audioCategory = "Slaps";
                selectedTargetName = "Global";
                currentSubMenu = "Execute";
                CreateDynamicLayoutUI();
            });
            if (slapsBtn != null) audioMenuButtons.Add(slapsBtn);

            string[] romanNumerals = new string[] { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            for (int a = 0; a < 10; a++)
            {
                string assetKey = "Asset " + (a + 1);
                string buttonLabel = "Asset " + romanNumerals[a];
                
                if (assetCuaStorables.ContainsKey(assetKey) && assetCuaStorables[assetKey] != null)
                {
                    string chosenUid = assetCuaStorables[assetKey].val;
                    if (!string.IsNullOrEmpty(chosenUid) && chosenUid != "None") buttonLabel = chosenUid; // WYTYCZNA: Kafelek przejmuje prawdziwą nazwę obiektu!
                }
                
                UIDynamicButton assetBtn = AddButton(buttonLabel, true, assetGreen, () =>
                {
                    audioCategory = "Assets";
                    selectedTargetName = assetKey; 
                    currentSubMenu = "Execute";
                    CreateDynamicLayoutUI();
                });
                if (assetBtn != null) audioMenuButtons.Add(assetBtn);
            }
        }

        private void RenderAudioSubCategoryPicker()
        {
            ClearAudioMenuButtons();
            Atom target = SuperController.singleton.GetAtomByUid(selectedAudioPersonUid);
            if (target == null)
            {
                selectedAudioPersonUid = null; selectedAudioPersonName = null; currentSubMenu = "Audio_Home"; CreateDynamicLayoutUI(); return;
            }
            Color backRed, menuBlue, whiteTxt;
            ColorUtility.TryParseHtmlString("#c02436", out backRed);
            ColorUtility.TryParseHtmlString("#2255ee", out menuBlue);
            ColorUtility.TryParseHtmlString("#f5f5f5", out whiteTxt); // WYTYCZNA: Kategorie zyskują kolor #f5f5f5
            
            UIDynamicButton backBtn = AddButton("Back to Previous", false, backRed, () => { GoBackMenuLogic(); });
            if (backBtn != null) audioMenuButtons.Add(backBtn);
            UIDynamicButton mainMenuBtn = AddButton("Go to Main Menu", true, menuBlue, () => { currentSubMenu = "Main"; currentTab = ""; selectedTargetName = ""; audioCategory = ""; CreateDynamicLayoutUI(); });
            if (mainMenuBtn != null) audioMenuButtons.Add(mainMenuBtn);

            string[] leftColumn = new string[] { "Breathing", "Licking", "Handjob" };
            string[] rightColumn = new string[] { "Oral", "Thrust", "Orgasm" };
            for (int row = 0; row < 3; row++)
            {
                string leftCategory = leftColumn[row]; string internalLeftCat = leftCategory; 
                if (leftCategory == "Oral") internalLeftCat = "Blowjob"; if (leftCategory == "Thrust") internalLeftCat = "Penetration";
                UIDynamicButton leftBtn = AddButton(leftCategory, false, whiteTxt, () => { selectedTargetName = selectedAudioPersonName; audioCategory = internalLeftCat; currentSubMenu = "Execute"; CreateDynamicLayoutUI(); });
                if (leftBtn != null) audioMenuButtons.Add(leftBtn);

                string rightCategory = rightColumn[row]; string internalRightCat = rightCategory;
                if (rightCategory == "Oral") internalRightCat = "Blowjob"; if (rightCategory == "Thrust") internalRightCat = "Penetration";
                UIDynamicButton rightBtn = AddButton(rightCategory, true, whiteTxt, () => { selectedTargetName = selectedAudioPersonName; audioCategory = internalRightCat; currentSubMenu = "Execute"; CreateDynamicLayoutUI(); });
                if (rightBtn != null) audioMenuButtons.Add(rightBtn);
            }
        }

        private void RenderPersonPicker(Action<string> onSelect)
        {
            Color personPink = new Color(0.945f, 0.396f, 0.945f); 
            List<Atom> sceneAtoms = SuperController.singleton.GetAtoms();
            for (int i = 1; i <= 4; i++)
            {
                string key = "Person " + i;
                bool isRight = (i % 2 == 0);
                string displayName = key; 
                for (int a = 0; a < sceneAtoms.Count; a++)
                {
                    Atom atom = sceneAtoms[a]; string atomKey;
                    if (atom != null && personKeyByAtomUid.TryGetValue(atom.uid, out atomKey) && atomKey == key && !string.IsNullOrEmpty(atom.name)) { displayName = atom.name; break; }
                }
                AddButton(displayName, isRight, personPink, () => { onSelect(key); });
            }
        }
        private void RenderAudioAssets()
        {
            Color assetGreen = new Color(0.0f, 0.75f, 0.0f);
            AddButton("Asset", false, assetGreen, () => { selectedTargetName = "Asset"; currentSubMenu = "Execute"; CreateDynamicLayoutUI(); });
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
                case "Slaps": RenderImpactAudioExecute(); break; 
                default: RenderSimpleCategoryAudioExecute(audioCategory); break;
            }
        }

        private void RenderPersonsAudioExecute()
        {
            Color textTheme = new Color(0f, 0.75f, 0.9f);
            AddSlider(GetFloat("AudVolume"), false, textTheme); AddSlider(GetFloat("AudAmplification"), true, textTheme);
            AddSlider(GetFloat("AudPitch"), false, textTheme); AddSlider(GetFloat("AudInterval"), true, textTheme);
            AddToggle(GetBool("Aud3D"), false);
            AudioBank bank = personAudioBanks.ContainsKey(selectedTargetName) ? personAudioBanks[selectedTargetName] : null;
            RenderAudioFilePicker(bank);
        }
        private void RenderAssetsAudioExecute()
        {
            Color textTheme; ColorUtility.TryParseHtmlString("#2d2d2d", out textTheme); 
            if (assetCuaStorables.ContainsKey(selectedTargetName))
            {
                var chooser = assetCuaStorables[selectedTargetName];
                List<string> foundObjects = new List<string> { "None" };
                List<Atom> sceneAtoms = SuperController.singleton.GetAtoms();
                for (int i = 0; i < sceneAtoms.Count; i++)
                {
                    Atom a = sceneAtoms[i]; if (a == null || string.IsNullOrEmpty(a.uid)) continue;
                    string lowUid = a.uid.ToLowerInvariant();
                    if (lowUid.Contains("windowcamera") || lowUid.Contains("navigation") || lowUid.Contains("corecontrol") || lowUid.Contains("subscene") || a.type == "Person" || a.type.Contains("Controller")) continue;
                    foundObjects.Add(a.uid);
                }
                string currentVal = chooser.val; chooser.choices = foundObjects; chooser.val = foundObjects.Contains(currentVal) ? currentVal : "None";
                AddPopup(chooser, false);
                JSONStorableBool toggle3D = GetBool("As3D"); if (toggle3D != null) toggle3D.name = "3D Audio";
                AddToggle(toggle3D, true);
            }
            JSONStorableFloat volParam = GetFloat("AsVolume"); JSONStorableFloat ampParam = GetFloat("AsAmplification");
            JSONStorableFloat pitParam = GetFloat("AsPitch"); JSONStorableFloat intParam = GetFloat("AsInterval");
            if (volParam != null) volParam.name = "Volume"; if (ampParam != null) ampParam.name = "Amplification";
            if (pitParam != null) pitParam.name = "Pitch"; if (intParam != null) intParam.name = "Interval Speed";
            AddSlider(volParam, false, textTheme); AddSlider(ampParam, true, textTheme);
            AddSlider(pitParam, false, textTheme); AddSlider(intParam, true, textTheme);
            AudioBank bank = assetAudioBanks.ContainsKey(selectedTargetName) ? assetAudioBanks[selectedTargetName] : null;
            RenderAudioFilePicker(bank);
        }

        private void RenderImpactAudioExecute()
        {
            Color textTheme; ColorUtility.TryParseHtmlString("#2d2d2d", out textTheme); 
            JSONStorableFloat volParam = GetFloat("AudImVolume"); JSONStorableFloat ampParam = GetFloat("AudImAmplification");
            JSONStorableFloat pitParam = GetFloat("AudImPitch"); JSONStorableFloat intParam = GetFloat("AudImInterval");
            JSONStorableBool toggle3D = GetBool("AudIm3D");
            if (volParam != null) volParam.name = "Volume"; if (ampParam != null) ampParam.name = "Amplification";
            if (pitParam != null) pitParam.name = "Pitch"; if (intParam != null) intParam.name = "Interval Speed";
            if (toggle3D != null) toggle3D.name = "3D Audio";
            AddSlider(volParam, false, textTheme); AddSlider(ampParam, true, textTheme);
            AddSlider(pitParam, false, textTheme); AddSlider(intParam, true, textTheme);
            AddToggle(toggle3D, false);
            RenderAudioFilePicker(impactAudioBank);
        }

        private void RenderSimpleCategoryAudioExecute(string category)
        {
            if (!simpleAudioCategoryPrefix.ContainsKey(category)) return;
            string prefix = simpleAudioCategoryPrefix[category];
            Color textTheme; ColorUtility.TryParseHtmlString("#2d2d2d", out textTheme); 
            JSONStorableFloat volParam = GetFloat(prefix + "Volume"); JSONStorableFloat ampParam = GetFloat(prefix + "Amplification");
            JSONStorableFloat pitParam = GetFloat(prefix + "Pitch"); JSONStorableFloat intParam = GetFloat(prefix + "Interval");
            JSONStorableBool toggle3D = GetBool(prefix + "3D");
            if (volParam != null) volParam.name = "Volume"; if (ampParam != null) ampParam.name = "Amplification";
            if (pitParam != null) pitParam.name = "Pitch"; if (intParam != null) intParam.name = "Interval Speed";
            if (toggle3D != null) toggle3D.name = "3D Audio";
            AddSlider(volParam, false, textTheme); AddSlider(ampParam, true, textTheme);
            AddSlider(pitParam, false, textTheme); AddSlider(intParam, true, textTheme);
            AddToggle(toggle3D, false);
            AudioBank bank; simpleAudioBanks.TryGetValue(category + "|" + selectedTargetName, out bank);
            RenderAudioFilePicker(bank);
        }
        private void RenderAudioFilePicker(AudioBank bank)
        {
            if (bank == null) return;
            Color textTheme, whiteBtnTxt;
            ColorUtility.TryParseHtmlString("#2d2d2d", out textTheme); ColorUtility.TryParseHtmlString("#f5f5f5", out whiteBtnTxt); 
            string statusReport = (bank.Clips.Count == 0) ? "Empty (0 Files loaded)" : bank.Clips.Count + " Active audio file(s) loaded";
            AddSubHeader(statusReport, textTheme, false);
            AddButton("Import file", true, whiteBtnTxt, () => { SuperController.singleton.GetMediaPathDialog((string path) => { if (!string.IsNullOrEmpty(path)) bank.ImportFile(path); CreateDynamicLayoutUI(); }, "wav|ogg|mp3", DefaultAudioFolder); });
            AddButton("Import folder", true, whiteBtnTxt, () => { SuperController.singleton.GetDirectoryPathDialog((string path) => { if (!string.IsNullOrEmpty(path)) bank.ImportFolder(path); CreateDynamicLayoutUI(); }, DefaultAudioFolder); });
            AddButton("Clear all files", true, whiteBtnTxt, () => { bank.ClearAll(); CreateDynamicLayoutUI(); });
        }

        private void RenderHandjobExecute()
        {
            Color deepPink = new Color(0.85f, 0.1f, 0.45f);
            AddToggle(GetBool("HJEnableLeft"), false); AddToggle(GetBool("HJEnableRight"), true);
            AddSlider(GetFloat("HJVelLeft"), false, deepPink); AddSlider(GetFloat("HJVelRight"), true, deepPink);
            AddSlider(GetFloat("HJIntLeft"), false, deepPink); AddSlider(GetFloat("HJIntRight"), true, deepPink);
        }

        private void RenderBreathingExecute()
        {
            Color breathingCyan = new Color(0.05f, 0.5f, 0.65f);
            JSONStorableBool autoCheck = GetBool("RLAutoBreathing");
            bool isAuto = (autoCheck != null && autoCheck.val);
            if (isAuto)
            {
                string idL = "warn_l_" + Guid.NewGuid().ToString("N"); JSONStorableString storL = new JSONStorableString(idL, "Auto Breathing System ACTIVE");
                UIDynamicTextField fldL = CreateTextField(storL, false);
                if (fldL != null) { fldL.height = 35f; Text txt = fldL.GetComponentInChildren<Text>(); if (txt != null) { txt.color = Color.red; txt.alignment = TextAnchor.MiddleCenter; } Image img = fldL.GetComponent<Image>(); if (img != null) img.color = new Color(0.06f, 0.04f, 0.04f); activeDynamicElements.Add(() => RemoveTextField(fldL)); }
                string idR = "warn_r_" + Guid.NewGuid().ToString("N"); JSONStorableString storR = new JSONStorableString(idR, "(Manual Overrides Locked)");
                UIDynamicTextField fldR = CreateTextField(storR, true);
                if (fldR != null) { fldR.height = 35f; Text txt = fldR.GetComponentInChildren<Text>(); if (txt != null) { txt.color = Color.red; txt.alignment = TextAnchor.MiddleCenter; } Image img = fldR.GetComponent<Image>(); if (img != null) img.color = new Color(0.06f, 0.04f, 0.04f); activeDynamicElements.Add(() => RemoveTextField(fldR)); }
            }
            AddToggle(pluginBools["BREnable"], false); if (!isAuto) AddButton("", true, Color.black, null, false);
            AddSlider(GetFloat("BRVelocity"), false, breathingCyan); AddSlider(GetFloat("BRIntense"), true, breathingCyan);
        }

        private void RenderBlowjobExecute()
        {
            Color darkPurple = new Color(0.45f, 0.1f, 0.65f);
            AddToggle(GetBool("IKEnable"), false); AddButton("", true, Color.black, null, false);
            AddSlider(GetFloat("BJVelocity"), false, darkPurple); AddSlider(GetFloat("BJIntense"), true, darkPurple);
        }

        private void RenderPenetrationExecute()
        {
            Color deepRed = new Color(0.65f, 0.05f, 0.05f);
            AddToggle(GetBool("PNEnable"), false); AddButton("", true, Color.black, null, false);
            AddSlider(GetFloat("PNVelocity"), false, deepRed); AddSlider(GetFloat("PNIntense"), true, deepRed);
        }

        private void RenderGazeExecute()
        {
            Color darkEmerald = new Color(0.02f, 0.40f, 0.20f);
            AddToggle(GetBool("GZEnable"), false); AddButton("", true, Color.black, null, false);
            AddSlider(GetFloat("GZHeadWeight"), false, darkEmerald); AddSlider(GetFloat("GZChestWeight"), true, darkEmerald);
        }
        private void RenderExpressionExecute()
        {
            Color violet = new Color(0.55f, 0.25f, 0.75f);
            for (int i = 1; i <= 4; i++)
            {
                string key = "Person " + i; bool rightSide = (i % 2 == 0);
                AddSubHeader("CONFIG FOR: " + key.ToUpper(), violet, rightSide);
                if (personalityStorables.ContainsKey(key)) AddPopup(personalityStorables[key], rightSide);
                if (energyStorables.ContainsKey(key)) AddSlider(energyStorables[key], rightSide, violet);
                if (moveStorables.ContainsKey(key)) AddPopup(moveStorables[key], rightSide);
            }
        }

        private void RenderRealismExecute()
        {
            Color textTheme; ColorUtility.TryParseHtmlString("#2d2d2d", out textTheme);
            
            string[] realismKeys = { 
                "RLAutoBreathing", "RLAutoBlinking", "RLAutoClothing", "RLAutoLipSync", 
                "RLAutoHandMovements", "RLAutoEnvironmentSound", "RLAutoPhysics", "RLAutoTransitions", 
                "RLAutoTessellation", "RLAutoFreezePose", "RLAutoMovement", "RLAutoNaturalMotion" 
            };
            string[] toggleLabels = {
                "Auto Breathing system", "Auto Blinking system", "Auto Clothing system", "Auto LipSync system",
                "Auto Hand Movements system", "Auto Environment Sound system", "Auto Physics system", "Auto Transitions system",
                "Auto Tessellation system", "Auto Freeze Pose system", "Auto Movement system", "Auto Natural Motion system"
            };

            for (int i = 0; i < realismKeys.Length; i++) 
            { 
                JSONStorableBool toggleStorable = GetBool(realismKeys[i]);
                if (toggleStorable != null) toggleStorable.name = toggleLabels[i];
                
                bool isRight = (i % 2 != 0); 
                AddToggle(toggleStorable, isRight); 
            }
        }


        private void GoBackMenuLogic()
        {
            if (currentSubMenu == "Execute")
            {
                if (currentTab == "Audio") { if (audioCategory == "Slaps") currentSubMenu = "Audio_Home"; else currentSubMenu = "Audio_SubCategoryPicker"; }
                else if (currentTab == "Breathing" || currentTab == "Penetration" || currentTab == "Gaze") currentSubMenu = "Select_Person";
                else { currentSubMenu = "Main"; currentTab = ""; selectedTargetName = ""; }
            }
            else if (currentSubMenu == "Audio_SubCategoryPicker") { selectedAudioPersonUid = null; selectedAudioPersonName = null; currentSubMenu = "Audio_Home"; }
            else if (currentSubMenu == "Audio_Home" || currentSubMenu == "Select_Person") { currentSubMenu = "Main"; currentTab = ""; selectedTargetName = ""; audioCategory = ""; }
            CreateDynamicLayoutUI();
        }

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
                canvas.gameObject.AddComponent<GraphicRaycaster>();

                GameObject panelObj = new GameObject("HUD_BackgroundPanel");
                panelObj.transform.SetParent(overlayCanvas.transform, false);

                Image panelBg = panelObj.AddComponent<Image>();
                panelBg.enabled = false; 
                panelBg.raycastTarget = false; 

                RectTransform panelRect = panelObj.GetComponent<RectTransform>();
                panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = Vector2.one;
                
                // WYTYCZNA KIEROWNIKA: Przesuwamy offset z -5 na -10 dla idealnego marginesu od krawędzi ekranu!
                panelRect.anchoredPosition = new Vector2(-10, -10);
                panelRect.sizeDelta = new Vector2(340, 840); // Szerokość dopasowana do myślników

                InitHudPanel(panelObj.transform);

                overlayCanvas.SetActive(true);
            }
            catch (Exception e)
            {
                SuperController.LogError("[HUD Create Error] " + e.Message);
            }
        }

        private void InitHudPanel(Transform hudCanvasTransform)
        {
            hudFont = Resources.GetBuiltinResource(typeof(Font), "Arial.ttf") as Font;

            hudRoot = new GameObject("UltraHudRoot");
            hudRoot.transform.SetParent(hudCanvasTransform, false);

            RectTransform rt = hudRoot.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; 
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.offsetMin = new Vector2(10f, 10f);
            rt.offsetMax = new Vector2(-10f, -10f);

            VerticalLayoutGroup vlg = hudRoot.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true; 
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false; 
            vlg.spacing = 1.5f; 

            ContentSizeFitter csf = hudRoot.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            hudLineObjects.Clear();
            hudLineLabels.Clear();
            hudLineTextGetters.Clear();

            // WYTYCZNA KIEROWNIKA: Generujemy 31 wierszy z zamrożonym, działającym komponentem Button!
            for (int i = 0; i < 31; i++)
            {
                CreatePermanentHudLine();
            }
        }

        private void CreatePermanentHudLine()
        {
            GameObject lineObj = new GameObject("HudLine");
            lineObj.transform.SetParent(hudRoot.transform, false);

            // Tło wiersza - tworzymy czystą, białą teksturę jako cel dla myszy
            Image bg = lineObj.AddComponent<Image>();
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            bg.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            bg.color = new Color(0.08f, 0.08f, 0.10f, 0.35f); // Stan spoczynku: Elegancki, przezroczysty grafit
            bg.raycastTarget = true;

            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(lineObj.transform, false);
            RectTransform trt = textObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12f, 2f); 
            trt.offsetMax = new Vector2(-12f, -2f);

            Text label = textObj.AddComponent<Text>();
            label.font = hudFont;
            label.fontSize = 15; 
            label.color = Color.white;
            label.supportRichText = true; 
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false; 

            LayoutElement le = lineObj.AddComponent<LayoutElement>();
            le.preferredHeight = 22f;

            Button btn = lineObj.AddComponent<Button>();
            btn.targetGraphic = bg;

            // Blokujemy ColorBlock na jednolity stan spoczynku, aby nie walczył ze skryptem Hovera
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            cb.colorMultiplier = 1f;
            btn.colors = cb;

            // --- PANCERNE WSTRZYKNIĘCIE NASZEGO AUTORSKIEGO SKRYPTU HOVERA Z CZĘŚCI 20E ---
            ButtonTextHover textHoverScript = lineObj.AddComponent<ButtonTextHover>();
            textHoverScript.targetText = label;
            textHoverScript.targetImage = bg;
            textHoverScript.normalColor = Color.white; // Kolor czcionki w spoczynku
            textHoverScript.hoverTextColor = new Color(0.95f, 0.95f, 0.95f, 1f); // Kolor czcionki przy najechaniu (Matowa biel)
            textHoverScript.normalBgColor = new Color(0.08f, 0.08f, 0.10f, 0.35f); // Tło w spoczynku (Przezroczysty grafit)
            textHoverScript.hoverBgColor = new Color(0.3137f, 0.3137f, 0.3137f, 0.85f); // Nasz hollywoodzki Hover #505050!

            lineObj.SetActive(false);

            hudLineLabels.Add(label);
            hudLineTextGetters.Add(null); 
            hudLineObjects.Add(lineObj);
        }

        private void BuildHUDContentText(List<Atom> allAtoms)
        {
            if (overlayTextInfo == null) return;
            string hudText = "<color=#2255ee><b>------------------Ultra Scene Controller------------------\n</b></color>\n";
            for (int p = 1; p <= 4; p++)
            {
                string key = "Person " + p; string realName = key;
                for (int i = 0; i < allAtoms.Count; i++) { Atom a = allAtoms[i]; string aKey; if (a != null && personKeyByAtomUid.TryGetValue(a.uid, out aKey) && aKey == key && !string.IsNullOrEmpty(a.name)) { realName = a.name; break; } }
                hudText += "<color=#f165f1><b>" + realName + "</b></color>\n";
                hudText += " Personality choice: <color=#00e5ff>[" + personPersonality[key] + "]</color>\n";
                hudText += " Aarousal: <color=#ffea00>[" + personEnergyLevel[key].ToString("F0") + "%]</color>\n";
                hudText += " Move: <color=#00ff66>[" + personMoveTarget[key] + "]</color>\n";
            }
            hudText += "<color=#d62a2e><b>------------------Realism------------------\n</b></color>\n";
            string[] displayRealismNames = { 
                "Auto Breathing system", 
                "Auto Licking system", 
                "Auto Foreskin system", 
                "Auto Sucking system", 
                "Auto Hand Movements system", 
                "Auto Penetration Sound system", 
                "Auto Slap system", 
                "Auto Orgasm system", 
                "Auto Normalize Audio system", 
                "Auto Tessellation system", 
                "Auto Dynamic Skin Wetness system", 
                "Auto Reset Joints system", 
                "Auto Freeze Pose system", 
                "Auto Movement system", 
                "Auto Natural Motion system", 
                "Auto Micro Muscle Drift system" 
            };
            string[] realismKeys = { "RLAutoBreathing", "RLAutoLicking", "RLAutoForeskin", "RLAutoSucking", "RLAutoHandMovements", "RLAutoPenetrationSound", "RLAutoSlap", "RLAutoOrgasm", "RLAutoNormalizeAudio", "RLAutoTessellation", "RLAutoDynamicSkinWetness", "RLAutoResetJoints", "RLAutoFreezePose", "RLAutoMovement", "RLAutoNaturalMotion", "RLAutoMicroMuscleDrift" };
            for (int r = 0; r < realismKeys.Length; r++) { JSONStorableBool toggle = GetBool(realismKeys[r]); bool state = (toggle != null && toggle.val); string statusLabel = state ? "<color=#00ff00><b>[ON]</b></color>" : "<color=#888888>[OFF]</color>"; hudText += " " + displayRealismNames[r] + ": " + statusLabel + "\n"; }
            overlayTextInfo.fontSize = 15; overlayTextInfo.lineSpacing = 1.25f; overlayTextInfo.supportRichText = true; overlayTextInfo.text = hudText;
        }



        private void SetBtnColor(UIDynamicButton element, Color c) 
        { 
            if (element == null || element.button == null) return; 
            Text txt = element.GetComponentInChildren<Text>(); if (txt != null) txt.color = c; 
            Image img = element.GetComponent<Image>(); 
            if (img != null) 
            { 
                Color baseGray, hoverBgBlack, pressedDark, hoverTxtWhite;
                ColorUtility.TryParseHtmlString("#2d2d2d", out baseGray); ColorUtility.TryParseHtmlString("#414141", out hoverBgBlack); 
                ColorUtility.TryParseHtmlString("#1a1a1a", out pressedDark); ColorUtility.TryParseHtmlString("#f5f5f5", out hoverTxtWhite); 
                img.color = baseGray; 
                ColorBlock cb = element.button.colors; cb.normalColor = Color.white; cb.highlightedColor = Color.white; cb.pressedColor = pressedDark; cb.colorMultiplier = 1f; element.button.colors = cb;
                ButtonTextHover textHoverScript = element.gameObject.GetComponent<ButtonTextHover>();
                if (textHoverScript == null) textHoverScript = element.gameObject.AddComponent<ButtonTextHover>();
                textHoverScript.targetText = txt; textHoverScript.targetImage = img; textHoverScript.normalColor = c; 
                textHoverScript.hoverTextColor = hoverTxtWhite; textHoverScript.normalBgColor = baseGray; textHoverScript.hoverBgColor = hoverBgBlack; 
            } 
        }

        public class ButtonTextHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
        {
            public Text targetText; public Image targetImage;
            public Color normalColor = Color.white; public Color hoverTextColor = Color.white;
            public Color normalBgColor = Color.gray; public Color hoverBgColor = Color.black;
            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) { if (targetText != null) targetText.color = hoverTextColor; if (targetImage != null) targetImage.color = hoverBgColor; }
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) { if (targetText != null) targetText.color = normalColor; if (targetImage != null) targetImage.color = normalBgColor; }
            void OnDisable() { if (targetText != null) targetText.color = normalColor; if (targetImage != null) targetImage.color = normalBgColor; }
        }

        private class AudioBank
        {
            private readonly JSONStorableString pathsStorable;
            public List<NamedAudioClip> Clips = new List<NamedAudioClip>();
            public AudioBank(JSONStorableString backingStorable) { pathsStorable = backingStorable; }
            public void RebuildFromStorable()
            {
                Clips.Clear(); if (pathsStorable == null || string.IsNullOrEmpty(pathsStorable.val)) return;
                string[] uids = pathsStorable.val.Split('|');
                for (int i = 0; i < uids.Length; i++) { string uid = uids[i]; if (string.IsNullOrEmpty(uid)) continue; NamedAudioClip clip = LoadClip(uid); if (clip != null) Clips.Add(clip); }
            }
            public void ImportFolder(string folderPath) { string[] files = SuperController.singleton.GetFilesAtPath(folderPath); if (files != null) { for (int i = 0; i < files.Length; i++) AddIfAudio(files[i]); } SyncStorable(); }
            public void ImportFile(string path) { AddIfAudio(path); SyncStorable(); }
            public void ClearAll() { Clips.Clear(); SyncStorable(); }
            public NamedAudioClip GetRandomClip() { if (Clips.Count == 0) return null; int idx = UnityEngine.Random.Range(0, Clips.Count); return Clips[idx]; }
            private void AddIfAudio(string path) { if (string.IsNullOrEmpty(path)) return; string lower = path.ToLowerInvariant(); bool isAudio = lower.EndsWith(".wav") || lower.EndsWith(".ogg") || lower.EndsWith(".mp3"); if (!isAudio) return; NamedAudioClip clip = LoadClip(path); if (clip != null && !Clips.Contains(clip)) Clips.Add(clip); }
            private static NamedAudioClip LoadClip(string rawPath) { string normalized = SuperController.singleton.NormalizeLoadPath(rawPath); NamedAudioClip existing = URLAudioClipManager.singleton.GetClip(normalized); if (existing != null) return existing; return URLAudioClipManager.singleton.QueueClip(normalized); }
            private void SyncStorable() { if (pathsStorable == null) return; List<string> uids = new List<string>(); for (int i = 0; i < Clips.Count; i++) uids.Add(Clips[i].uid); pathsStorable.val = string.Join("|", uids.ToArray()); }
        }
    } // <-- TA KLAMRA ZAMYKA WYŁĄCZNIE KLASĘ GŁÓWNĄ UltraSceneController, ALE ZOSTAWIA OTWARTY NAMESPACE!

    // =====================================================================
    // GLOBALNE MODUŁY WYKONAWCZE (WEWNĄTRZ NAMESPACE ULTRASCENE)
    // =====================================================================
    public class UltraAudioModule { public float volume, pitch; public bool is3DSound; public UltraAudioModule(UltraSceneController p, AudioSource s) {} public void PlayTargetSound(string c, Vector3 pos) {} public void NormalizeLoadedAudio() {} public void ClearAllFiles() {} }
    public class UltraLickingModule { public bool isEnabled; public float lickingSpeed; public UltraLickingModule(UltraSceneController p) {} public void UpdateModule(Atom s, Atom t) {} }
    public class UltraHandjobModule { public bool isEnabled; public float handjobSpeed, handjobIntense; public UltraHandjobModule(UltraSceneController p) {} public void UpdateModule(Atom s, Atom t) {} }
    public class UltraBlowjobModule { public bool isEnabled; public float suckSpeed; public UltraBlowjobModule(UltraSceneController p) {} public void UpdateModule(Atom s, Atom t) {} }
    public class UltraPenetrationModule { public bool playCollisionSounds; public float penetrationIntense; public UltraPenetrationModule(UltraSceneController p) {} public void UpdateModule(Atom s, Atom t, int idx) {} }
    public class UltraRealismModule { public bool autoBreathing; public UltraRealismModule(UltraSceneController p) {} public void UpdateModule(List<Atom> l) {} }
    public class UltraOrgasmSystem { public float currentExcitement; public UltraOrgasmSystem(UltraSceneController p, UltraBlowjobModule b) {} public void UpdateSystem(Atom s, Atom t, string path) {} }
    public class UltraAmbientModule { public UltraAmbientModule(UltraSceneController p) {} }
    public class UltraInteractionModule { public UltraInteractionModule(UltraSceneController p) {} }
    public class UltraMotionModule { public UltraMotionModule(UltraSceneController p) {} }
    public class UltraReactionModule { public UltraReactionModule(UltraSceneController p) {} }
    public class UltraStateSystem { public UltraStateSystem(UltraSceneController p, UltraMotionModule m) {} }

    public class UltraBreathingModule 
    { 
        private UltraSceneController plugin; private AudioSource audioSource; private float breathTimer = 0f; private float currentSpeed = 1.2f; private float currentIntensity = 0.8f;
        public UltraBreathingModule(UltraSceneController p, AudioSource a) { plugin = p; audioSource = a; } 
        public void UpdateModule(Atom personAtom) 
        { 
            if (personAtom == null || personAtom.type != "Person") return;
            JSONStorableBool breathingMasterSwitch = plugin.GetBool("BREnable"); if (breathingMasterSwitch != null && !breathingMasterSwitch.val) return; 
            JSONStorableBool autoBreathingToggle = plugin.GetBool("RLAutoBreathing"); bool isAutoBreathingActive = (autoBreathingToggle != null && autoBreathingToggle.val);
            if (isAutoBreathingActive)
            {
                float baseExertion = 0.0f;
                JSONStorableBool bjEnable = plugin.GetBool("IKEnable"); if (bjEnable != null && bjEnable.val) baseExertion = Mathf.Max(baseExertion, 0.4f);
                JSONStorableBool hjLeft = plugin.GetBool("HJEnableLeft"); JSONStorableBool hjRight = plugin.GetBool("HJEnableRight"); if ((hjLeft != null && hjLeft.val) || (hjRight != null && hjRight.val)) baseExertion = Mathf.Max(baseExertion, 0.3f);
                JSONStorableBool pnEnable = plugin.GetBool("PNEnable"); if (pnEnable != null && pnEnable.val) baseExertion = Mathf.Max(baseExertion, 0.7f);
                float targetSpeed = Mathf.Lerp(1.2f, 2.8f, baseExertion); float targetIntensity = Mathf.Lerp(0.7f, 1.3f, baseExertion);
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 1.5f); currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, Time.deltaTime * 1.5f);
                JSONStorableFloat uiVel = plugin.GetFloat("BRVelocity"); JSONStorableFloat uiInt = plugin.GetFloat("BRIntense"); if (uiVel != null) uiVel.valNoCallback = currentSpeed; if (uiInt != null) uiInt.valNoCallback = currentIntensity;
            }
            else { JSONStorableFloat velStorable = plugin.GetFloat("BRVelocity"); JSONStorableFloat intStorable = plugin.GetFloat("BRIntense"); currentSpeed = (velStorable != null) ? velStorable.val : 1.2f; currentIntensity = (intStorable != null) ? intStorable.val : 0.8f; }
            breathTimer += Time.deltaTime * currentSpeed * 2.5f; float breathWave = Mathf.Sin(breathTimer);
            JSONStorable shapeStorable = personAtom.GetStorableByID("geometry"); if (shapeStorable == null) return;
            float chestValue = (breathWave * 0.45f * currentIntensity) + 0.5f; float stomachValue = (breathWave * 0.35f * currentIntensity) + 0.4f; float positiveWave = Mathf.Max(0f, breathWave) * currentIntensity;
            shapeStorable.SetFloatParamValue("Breathing Chest", chestValue); shapeStorable.SetFloatParamValue("Breathing Stomach", stomachValue); shapeStorable.SetFloatParamValue("Breathing Lips", positiveWave * 0.25f); shapeStorable.SetFloatParamValue("Breathing NoseIn", positiveWave * 0.20f); shapeStorable.SetFloatParamValue("Breathing NoseOut", (1f - positiveWave) * 0.20f);
        } 
    }
    // =====================================================================
    // CZĘŚĆ 2/3: MODUŁ WZROKU (Sakkady i inteligentne śledzenie)
    // =====================================================================
    public class UltraGazeModule 
    { 
        private UltraSceneController plugin;
        private Dictionary<string, float> nextLookTime = new Dictionary<string, float>();
        private Dictionary<string, Vector3> randomLookTarget = new Dictionary<string, Vector3>();
        private Dictionary<string, string> currentLookType = new Dictionary<string, string>(); 

        public UltraGazeModule(UltraSceneController p) { plugin = p; } 

        public void UpdateModule(Atom sourceAtom, List<Atom> allAtoms) 
        { 
            if (sourceAtom == null || sourceAtom.type != "Person") return;

            JSONStorableBool gazeEnabled = plugin.GetBool("GZEnable");
            if (gazeEnabled != null && !gazeEnabled.val) return;

            string uid = sourceAtom.uid;
            float now = Time.time;

            if (!nextLookTime.ContainsKey(uid)) nextLookTime[uid] = 0f;
            if (!randomLookTarget.ContainsKey(uid)) randomLookTarget[uid] = Vector3.zero;
            if (!currentLookType.ContainsKey(uid)) currentLookType[uid] = "Camera";

            if (now > nextLookTime[uid])
            {
                nextLookTime[uid] = now + UnityEngine.Random.Range(1.5f, 4.0f);
                float roll = UnityEngine.Random.value;

                if (roll < 0.4f) currentLookType[uid] = "Camera";
                else if (roll < 0.8f) currentLookType[uid] = "Person";
                else
                {
                    currentLookType[uid] = "Random";
                    Transform head = sourceAtom.GetComponentInChildren<Rigidbody>()?.transform;
                    Vector3 forwardPos = head != null ? head.position + head.forward * 2f : sourceAtom.transform.position + sourceAtom.transform.forward * 2f;
                    randomLookTarget[uid] = forwardPos + UnityEngine.Random.insideUnitSphere * 0.6f;
                }
            }

            Vector3 finalTargetPos = Vector3.zero;
            bool targetFound = false;

            if (currentLookType[uid] == "Camera")
            {
                Camera cam = Camera.main;
                if (cam != null) { finalTargetPos = cam.transform.position; targetFound = true; }
            }
            else if (currentLookType[uid] == "Person")
            {
                for (int i = 0; i < allAtoms.Count; i++)
                {
                    Atom candidate = allAtoms[i];
                    if (candidate != null && candidate.type == "Person" && candidate.uid != uid && candidate.on)
                    {
                        JSONStorable ctrl = candidate.GetStorableByID("headControl");
                        if (ctrl != null) { finalTargetPos = ctrl.transform.position; targetFound = true; break; }
                    }
                }
            }

            if (!targetFound) finalTargetPos = randomLookTarget[uid];

            JSONStorable lookAtStorable = sourceAtom.GetStorableByID("LookAt");
            if (lookAtStorable != null)
            {
                JSONStorableFloat headWeightParam = plugin.GetFloat("GZHeadWeight");
                JSONStorableFloat chestWeightParam = plugin.GetFloat("GZChestWeight");

                float headWeight = (headWeightParam != null) ? headWeightParam.val : 0.7f;
                float chestWeight = (chestWeightParam != null) ? chestWeightParam.val : 0.7f;

                lookAtStorable.SetBoolParamValue("enabled", true);
                
                float currentHeadWeight = lookAtStorable.GetFloatJSONParam("headWeight")?.val ?? 0f;
                float currentChestWeight = lookAtStorable.GetFloatJSONParam("chestWeight")?.val ?? 0f;

                lookAtStorable.SetFloatParamValue("headWeight", Mathf.Lerp(currentHeadWeight, headWeight, Time.deltaTime * 3.0f));
                lookAtStorable.SetFloatParamValue("chestWeight", Mathf.Lerp(currentChestWeight, chestWeight, Time.deltaTime * 1.5f));
                
                FreeControllerV3 targetController = sourceAtom.GetStorableByID("lookAtTargetControl") as FreeControllerV3;
                if (targetController != null)
                {
                    targetController.transform.position = Vector3.Lerp(targetController.transform.position, finalTargetPos, Time.deltaTime * 6.0f);
                }
            }
        } 
    }
    // =====================================================================
    // CZĘŚĆ DODATKOWA C1: MODUŁ MIMIKI TWARZY (Zmienne i pierwsza połowa)
    // =====================================================================
    public class UltraExpressionModule 
    { 
        private UltraSceneController plugin;
        private Dictionary<string, Dictionary<string, JSONStorableFloat>> personMorphCache = new Dictionary<string, Dictionary<string, JSONStorableFloat>>();
        private Dictionary<string, float> nextWinkTime = new Dictionary<string, float>();
        private Dictionary<string, float> winkDuration = new Dictionary<string, float>();
        private Dictionary<string, string> activeWinkSide = new Dictionary<string, string>();
 
        private const string ascoPath = "ascorad.asco_Expressions.12:/Custom/Atom/Person/Morphs/female/asco - Expressions/";
        private const string ashPath = "AshAuryn.AshAuryn_Sexpressions_2_Point_0.5:/Custom/Atom/Person/Morphs/female/ASHAURYN OFFICIAL/TOOLS/EXPRESSIONS/EYES/CLOSED/";
 
        public UltraExpressionModule(UltraSceneController p) { plugin = p; }

        public void UpdateModule(Atom personAtom, string personKey, float currentArousal, string currentAction, string personality) 
        { 
            if (personAtom == null || personAtom.type != "Person") return;
            JSONStorable geoComponent = personAtom.GetStorableByID("geometry");
            if (geoComponent == null) return;

            if (!personMorphCache.ContainsKey(personAtom.uid)) personMorphCache[personAtom.uid] = new Dictionary<string, JSONStorableFloat>();

            float normalizedArousal = Mathf.Clamp01(currentArousal / 100f);
            float arousalFactor = normalizedArousal * normalizedArousal;

            float targetDesire = 0f, targetExcitement = arousalFactor * 0.7f, targetHappy = 0f, targetSurprise = 0f, targetConfused = 0f;
            float targetPain = 0f, targetSnarlL = 0f, targetSnarlR = 0f, targetAfraid = 0f, targetContempt = 0f, targetFrown = 0f;
            float targetBedroomSmile = 0f, targetLovelySmile = 0f, targetLipBiteWide = 0f, targetExpressionA = 0f, targetExpressionB = 0f, targetUgh = 0f, targetUhOh = 0f;
            float targetWinkL = 0f, targetWinkR = 0f, tongueFlutter = 0f;

            if (arousalFactor > 0.5f) tongueFlutter = Mathf.Sin(Time.time * 6.0f) * 0.25f * arousalFactor;

            switch (personality)
            {
                case "Sensual / Romantic":
                    targetDesire = 0.5f + (arousalFactor * 0.2f); targetBedroomSmile = 0.35f * arousalFactor;
                    if (arousalFactor >= 0.5f) targetSnarlL = (arousalFactor - 0.5f) * 0.4f; 
                    if (currentAction == "Licking" && arousalFactor > 0.5f) targetBedroomSmile += tongueFlutter;
                    break;
                case "Shy / Surprised":
                    targetConfused = 0.3f + (arousalFactor * 0.4f); targetSurprise = 0.35f * arousalFactor;
                    break;
                case "Passionate / Ecstatic":
                    targetDesire = 0.7f; if (arousalFactor >= 0.5f) targetSnarlR = 0.15f + ((arousalFactor - 0.5f) * 0.2f);
                    break;
                case "Angry / Surprised":
                    if (arousalFactor < 0.5f) targetFrown = 0.5f; else { targetDesire = 0.5f; targetHappy = 0.25f; targetSnarlL = 0.2f; }
                    break;
                case "Fear / Scream":
                    targetSurprise = 0.65f * arousalFactor; targetPain = 0.65f * arousalFactor; targetUgh = 0.70f * arousalFactor;
                    break;
                case "Horny / Disgust":
                    targetDesire = 0.7f; if (arousalFactor >= 0.5f) targetSnarlL = 0.25f;
                    break;
                case "Intense / Surprised":
                    targetHappy = 0.45f; targetSurprise = 0.35f;
                    if (arousalFactor < 0.5f && UnityEngine.Random.value < 0.005f && Time.time > nextWinkTime[personAtom.uid])
                    {
                        activeWinkSide[personAtom.uid] = "Right"; winkDuration[personAtom.uid] = Time.time + 0.25f;
                        nextWinkTime[personAtom.uid] = Time.time + UnityEngine.Random.Range(8.0f, 18.0f);
                    }
                    if (arousalFactor >= 0.5f) targetSnarlR = 0.2f; 
                    break;
                case "Intense / Pain":
                    targetDesire = 0.55f; targetPain = 0.2f + (arousalFactor * 0.4f); targetLipBiteWide = 0.5f * arousalFactor; 
                    if (arousalFactor >= 0.5f) { targetSnarlL = 0.15f + ((arousalFactor - 0.5f) * 0.3f); targetExpressionB = 0.25f; }
                    break;
                case "Evil / Intense":
                    targetBedroomSmile = 0.40f * arousalFactor; targetLovelySmile = 0.35f * arousalFactor;
                    targetSnarlL = 0.15f * arousalFactor; targetSnarlR = 0.15f * arousalFactor; targetDesire = 0.7f;
                    break;
                case "Angry / Disgust":
                    targetFrown = 0.65f; targetContempt = 0.5f;
                    break;
                case "Concerned / Surprised":
                    targetConfused = 0.45f; targetSurprise = 0.35f;
                    break;
                case "Great / Surprised":
                    targetHappy = 0.65f; targetSurprise = 0.25f;
                    if (arousalFactor < 0.6f && UnityEngine.Random.value < 0.008f && Time.time > nextWinkTime[personAtom.uid])
                    {
                        activeWinkSide[personAtom.uid] = (UnityEngine.Random.value > 0.5f) ? "Left" : "Right";
                        winkDuration[personAtom.uid] = Time.time + 0.25f; nextWinkTime[personAtom.uid] = Time.time + UnityEngine.Random.Range(6.0f, 15.0f);
                    }
                    if (arousalFactor >= 0.5f) targetSnarlL = 0.15f;
                    break;
                default: 
                    targetDesire = arousalFactor * 0.3f;
                    if (arousalFactor < 0.4f && UnityEngine.Random.value < 0.003f && Time.time > nextWinkTime[personAtom.uid])
                    {
                        activeWinkSide[personAtom.uid] = "Left"; winkDuration[personAtom.uid] = Time.time + 0.25f;
                        nextWinkTime[personAtom.uid] = Time.time + UnityEngine.Random.Range(10.0f, 25.0f);
                    }
                    break;
            }

            targetAfraid = (personality == "Fear / Scream") ? 0.65f : 0f;
            float targetAngry = (personality == "Angry / Surprised" && arousalFactor < 0.5f) ? 0.5f : 0f;
            float targetConcentrate = (personality == "Concerned / Surprised") ? 0.45f : 0f;
            float targetFear = (personality == "Fear / Scream") ? 0.65f : 0f;
            float targetShock = (personality == "Fear / Scream") ? 0.5f : 0f;
            float targetDisgust = (personality == "Intense / Disgust" || personality == "Angry / Disgust") ? 0.6f : 0f;

            if (arousalFactor >= 1.0f) { targetDesire = 0.7f; targetPain = 0.5f; targetSnarlL = 0.35f; targetSnarlR = 0.35f; targetWinkL = 0.65f; targetWinkR = 0.65f; }
            else
            {
                if (!nextWinkTime.ContainsKey(personAtom.uid)) nextWinkTime[personAtom.uid] = Time.time + 10f;
                if (!winkDuration.ContainsKey(personAtom.uid)) winkDuration[personAtom.uid] = 0f;
                if (!activeWinkSide.ContainsKey(personAtom.uid)) activeWinkSide[personAtom.uid] = "None";
                if (Time.time < winkDuration[personAtom.uid]) { if (activeWinkSide[personAtom.uid] == "Left") targetWinkL = 1.0f; else if (activeWinkSide[personAtom.uid] == "Right") targetWinkR = 1.0f; }
                else activeWinkSide[personAtom.uid] = "None";
            }

            targetSurprise = Mathf.Min(targetSurprise, 0.70f); targetPain = Mathf.Min(targetPain, 0.70f);
            targetSnarlL = Mathf.Min(targetSnarlL, 0.70f); targetSnarlR = Mathf.Min(targetSnarlR, 0.70f);
            targetAfraid = Mathf.Min(targetAfraid, 0.70f); targetContempt = Mathf.Min(targetContempt, 0.70f);
            targetFrown = Mathf.Min(targetFrown, 0.70f); targetUgh = Mathf.Min(targetUgh, 0.70f);
            targetUhOh = Mathf.Min(targetUhOh, 0.70f); targetLipBiteWide = Mathf.Min(targetLipBiteWide, 0.70f);
            targetBedroomSmile = Mathf.Min(targetBedroomSmile, 0.70f); targetLovelySmile = Mathf.Min(targetLovelySmile, 0.70f);

            SetMorphValueSafe(geoComponent, "Afraid", targetAfraid); SetMorphValueSafe(geoComponent, "Angry", targetAngry);
            SetMorphValueSafe(geoComponent, "Concentrate", targetConcentrate); SetMorphValueSafe(geoComponent, "Confused", targetConfused);
            SetMorphValueSafe(geoComponent, "Contempt", targetContempt); SetMorphValueSafe(geoComponent, "Desire", targetDesire);
            SetMorphValueSafe(geoComponent, "Disgust", targetDisgust); SetMorphValueSafe(geoComponent, "Excitement", targetExcitement);
            SetMorphValueSafe(geoComponent, "Fear", targetFear); SetMorphValueSafe(geoComponent, "Frown", targetFrown);
            SetMorphValueSafe(geoComponent, "Happy", targetHappy); SetMorphValueSafe(geoComponent, "Pain", targetPain);
            SetMorphValueSafe(geoComponent, "Shock", targetShock); SetMorphValueSafe(geoComponent, "Surprise", targetSurprise);
            SetMorphValueSafe(geoComponent, "Snarl Left", targetSnarlL); SetMorphValueSafe(geoComponent, "Snarl Right", targetSnarlR);
            SetMorphValueSafe(geoComponent, ascoPath + "v12 asco - Bedroom Smile.vmi", targetBedroomSmile);
            SetMorphValueSafe(geoComponent, ascoPath + "v12 asco - Lovely Smile.vmi", targetLovelySmile);
            SetMorphValueSafe(geoComponent, ascoPath + "v12 asco - Lip Bite Wide.vmi", targetLipBiteWide);
            SetMorphValueSafe(geoComponent, ascoPath + "v12 asco - Expression A.vmi", targetExpressionA);
            SetMorphValueSafe(geoComponent, ascoPath + "v12 asco - Expression B.vmi", targetExpressionB);
            SetMorphValueSafe(geoComponent, ascoPath + "v12 asco - Ugh.vmi", targetUgh);
            SetMorphValueSafe(geoComponent, ascoPath + "v12 asco - Uh Oh.vmi", targetUhOh);
            SetMorphValueSafe(geoComponent, ashPath + "Eyes Wink L.vmi", targetWinkL); SetMorphValueSafe(geoComponent, ashPath + "Eyes Wink R.vmi", targetWinkR);
        }

        private void SetMorphValueSafe(JSONStorable geo, string morphUid, float value)
        {
            try { if (geo != null) { JSONStorableFloat morphParam = geo.GetFloatJSONParam(morphUid); if (morphParam != null) morphParam.val = Mathf.Lerp(morphParam.val, value, Time.deltaTime * 3.5f); } } catch {}
        }
    }
}