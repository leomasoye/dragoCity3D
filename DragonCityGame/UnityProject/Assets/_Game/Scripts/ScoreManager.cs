using System.Collections.Generic;
using UnityEngine;

namespace DragonCity
{
    public class ScoreManager : MonoBehaviour
    {
        private static ScoreManager _instance;
        public static ScoreManager Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                    _instance = FindAnyObjectByType<ScoreManager>();
#else
                    _instance = FindObjectOfType<ScoreManager>();
#endif
                    if (_instance == null && Application.isPlaying)
                    {
                        GameObject go = new GameObject("ScoreManager_Auto");
                        _instance = go.AddComponent<ScoreManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Score Values")]
        public int totalScore = 0;
        public int highScore = 0;
        public int buildingsDestroyed = 0;
        public int totalBuildings = 0;

        [Header("Speedrun Timer & Victory Condition")]
        public float gameTime = 0f;
        public bool isGameFinished = false;
        public bool timerRunning = false;
        public int maxComboAchieved = 1;
        public string finalTimeFormatted = "";
        public string speedGrade = "";
        public string speedGradeTitle = "";
        public Color speedGradeColor = Color.white;

        [Header("Combo System")]
        public int currentCombo = 0;
        public int comboMultiplier = 1;
        public float comboTimer = 0f;
        public float comboDuration = 4.8f;
        public int accumulatedComboScore = 0;

        [Header("Audio Feedback")]
        public bool enableProceduralAudio = true;
        private AudioSource audioSource;
        private AudioClip chimeClip;
        private AudioClip heavyImpactClip;

        // Visual animation states
        private float displayedScore = 0f;
        private float scorePunchScale = 1.0f;
        private float comboPunchScale = 1.0f;

        // Banner notifications
        private string activeBannerText = "";
        private string activeBannerSubtext = "";
        private float bannerTimer = 0f;
        private float bannerDuration = 2.4f;
        private Color bannerColor = Color.white;

        // Floating 3D/World Score Popups
        private class ScorePopup
        {
            public Vector3 worldPosition;
            public string text;
            public Color color;
            public float timer;
            public float maxLifetime;
            public float scale;
        }
        private List<ScorePopup> activePopups = new List<ScorePopup>();

        // Textures for UI drawing
        private Texture2D whitePixel;
        private GUIStyle scoreLabelStyle;
        private GUIStyle comboLabelStyle;
        private GUIStyle bannerTitleStyle;
        private GUIStyle bannerSubStyle;
        private GUIStyle popupStyle;
        private GUIStyle smallLabelStyle;

        // Ranks
        private static readonly (int threshold, string title, Color col)[] RANKS = new (int, string, Color)[]
        {
            (0, "Draconien Éveillé", new Color(0.7f, 0.75f, 0.8f)),
            (5000, "Pilleur Ardent", new Color(0.85f, 0.55f, 0.25f)),
            (15000, "Fléau Urbain", new Color(0.9f, 0.85f, 0.3f)),
            (35000, "Dévoreur de Cité", new Color(1f, 0.45f, 0.15f)),
            (75000, "Seigneur des Cendres", new Color(0.95f, 0.18f, 0.15f)),
            (150000, "APOCALYPSE DRACONIQUE", new Color(0.95f, 0.3f, 0.95f))
        };
        private int currentRankIndex = 0;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            highScore = PlayerPrefs.GetInt("DragonCity_HighScore", 0);

            if (enableProceduralAudio)
            {
                SetupProceduralAudio();
            }
        }

        private void Start()
        {
            if (totalBuildings <= 0)
            {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                var buildings = FindObjectsByType<DestructibleBuilding>(FindObjectsSortMode.None);
#else
                var buildings = FindObjectsOfType<DestructibleBuilding>();
#endif
                totalBuildings = buildings != null ? buildings.Length : 0;
            }

            NotifyDragonRankEvolution(currentRankIndex);
        }

        private void Update()
        {
            // 0. Speedrun Timer & Max Combo Tracking
            if (!isGameFinished)
            {
                if (timerRunning || buildingsDestroyed > 0 || totalScore > 0)
                {
                    timerRunning = true;
                    gameTime += Time.deltaTime;
                }
            }
            maxComboAchieved = Mathf.Max(maxComboAchieved, currentCombo);

            // Rebuild & restart shortcut
            if (isGameFinished && Input.GetKeyDown(KeyCode.R))
            {
                RestartCity();
            }

            // 1. Smoothly roll displayed score counter
            if (displayedScore < totalScore)
            {
                float step = Mathf.Max(120f, (totalScore - displayedScore) * 10f) * Time.deltaTime;
                displayedScore = Mathf.MoveTowards(displayedScore, totalScore, step);
            }

            // 2. Punch scale dampening
            scorePunchScale = Mathf.Lerp(scorePunchScale, 1.0f, Time.deltaTime * 6f);
            comboPunchScale = Mathf.Lerp(comboPunchScale, 1.0f, Time.deltaTime * 6f);

            // 3. Combo timer decay
            if (comboTimer > 0f)
            {
                comboTimer -= Time.deltaTime;
                if (comboTimer <= 0f)
                {
                    EndCombo();
                }
            }

            // 4. Banner timer
            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.deltaTime;
            }

            // 5. Update floating popups (rise & fade)
            for (int i = activePopups.Count - 1; i >= 0; i--)
            {
                var p = activePopups[i];
                p.worldPosition += Vector3.up * (8.5f * Time.deltaTime);
                p.timer -= Time.deltaTime;
                if (p.timer <= 0f)
                {
                    activePopups.RemoveAt(i);
                }
            }
        }

        public void SetTotalBuildings(int count)
        {
            totalBuildings = count;
        }

        public void ResetScore()
        {
            totalScore = 0;
            displayedScore = 0f;
            buildingsDestroyed = 0;
            currentCombo = 0;
            comboMultiplier = 1;
            comboTimer = 0f;
            currentRankIndex = 0;
            activePopups.Clear();

            // Reset Speedrun state
            isGameFinished = false;
            gameTime = 0f;
            timerRunning = false;
            maxComboAchieved = 1;
            finalTimeFormatted = "";
            speedGrade = "";

            NotifyDragonRankEvolution(0);
        }

        public void TriggerGameVictory()
        {
            isGameFinished = true;
            timerRunning = false;

            // Format final time: MM:SS.ff
            int minutes = Mathf.FloorToInt(gameTime / 60f);
            int seconds = Mathf.FloorToInt(gameTime % 60f);
            int fraction = Mathf.FloorToInt((gameTime * 100f) % 100f);
            finalTimeFormatted = string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, fraction);

            // Compute speed grade
            if (gameTime <= 120f)
            {
                speedGrade = "RANG S+";
                speedGradeTitle = "DIVINITÉ DU CHAOS";
                speedGradeColor = new Color(1f, 0.25f, 0.95f);
            }
            else if (gameTime <= 180f)
            {
                speedGrade = "RANG S";
                speedGradeTitle = "TEMPÊTE ARDENTE";
                speedGradeColor = new Color(1f, 0.85f, 0.15f);
            }
            else if (gameTime <= 270f)
            {
                speedGrade = "RANG A";
                speedGradeTitle = "FLÉAU LÉGENDAIRE";
                speedGradeColor = new Color(1f, 0.45f, 0.15f);
            }
            else if (gameTime <= 360f)
            {
                speedGrade = "RANG B";
                speedGradeTitle = "CONQUÉRANT URBAIN";
                speedGradeColor = new Color(0.3f, 0.85f, 1f);
            }
            else
            {
                speedGrade = "RANG C";
                speedGradeTitle = "DRAGON PERSÉVÉRANT";
                speedGradeColor = new Color(0.75f, 0.75f, 0.8f);
            }

            PlayHeavyImpact();
            ThirdPersonCamera.Instance?.TriggerShake(1.6f, 1.4f);
            TriggerBanner("🏆 VILLE 100% ANÉANTIE ! 🏆", $"Temps : {finalTimeFormatted} | Évaluation : {speedGrade}", speedGradeColor);

            Debug.Log($"[ScoreManager] VICTORY! All {totalBuildings} buildings demolished in {finalTimeFormatted}!");
        }

        public void RestartCity()
        {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            var builder = FindAnyObjectByType<CityBuilder>();
#else
            var builder = FindObjectOfType<CityBuilder>();
#endif
            if (builder != null)
            {
                builder.BuildCity();
            }
            ResetScore();
        }

        public void OnDamageTick(float damage)
        {
            // Minor score trickle while melting buildings
            int tickPoints = Mathf.Max(1, Mathf.RoundToInt(damage * 0.45f));
            AddScoreRaw(tickPoints);
        }

        public void OnBuildingDestroyed(Vector3 worldPos, float height, float volume)
        {
            buildingsDestroyed++;

            // 1. Calculate Base Points from scale & height
            int basePoints = Mathf.RoundToInt(height * 24f + Mathf.Sqrt(Mathf.Max(1f, volume)) * 6f);
            basePoints = Mathf.Clamp(basePoints, 350, 4500);

            // 2. Combo Progression
            currentCombo++;
            comboTimer = comboDuration;
            comboMultiplier = CalculateMultiplier(currentCombo);

            int earnedPoints = basePoints * comboMultiplier;
            accumulatedComboScore += earnedPoints;
            AddScoreRaw(earnedPoints);

            // Punch effects
            scorePunchScale = 1.35f;
            comboPunchScale = 1.45f;

            // 3. Play Satisfying Audio Chime
            PlayComboChime(comboMultiplier);

            // 4. Floating 3D World Score Popup
            string popupTxt = $"+{earnedPoints:N0}";
            if (comboMultiplier > 1)
            {
                popupTxt += $" (x{comboMultiplier})";
            }
            Color popupCol = GetComboColor(comboMultiplier);
            Vector3 spawnPos = worldPos + Vector3.up * (height * 0.4f);
            SpawnScorePopup(spawnPos, popupTxt, popupCol);

            // 5. Feat Evaluation (Skyscraper collapse, combo milestones)
            EvaluateDestructionFeats(height, comboMultiplier);

            // 6. Check Rank Advancement
            CheckRankProgression();

            // 7. Check Game Victory (100% of buildings demolished!)
            if (totalBuildings > 0 && buildingsDestroyed >= totalBuildings && !isGameFinished)
            {
                TriggerGameVictory();
            }
        }

        private int CalculateMultiplier(int combo)
        {
            if (combo >= 25) return 10;
            if (combo >= 18) return 8;
            if (combo >= 12) return 6;
            if (combo >= 8) return 5;
            if (combo >= 5) return 4;
            if (combo >= 3) return 3;
            if (combo >= 2) return 2;
            return 1;
        }

        private Color GetComboColor(int mult)
        {
            if (mult >= 8) return new Color(1f, 0.25f, 0.95f); // Neon Magenta
            if (mult >= 5) return new Color(1f, 0.35f, 0.1f);  // Solar Crimson
            if (mult >= 3) return new Color(1f, 0.85f, 0.1f);  // Blazing Gold
            if (mult >= 2) return new Color(0.2f, 0.95f, 0.4f);  // Electric Emerald
            return new Color(1f, 0.95f, 0.85f);
        }

        private void EndCombo()
        {
            if (currentCombo >= 3)
            {
                TriggerBanner($"COMBO TERMINÉ !", $"+{accumulatedComboScore:N0} POINTS ACCUMULÉS ({currentCombo} Bâtiments)", new Color(0.9f, 0.85f, 0.3f));
            }
            currentCombo = 0;
            comboMultiplier = 1;
            accumulatedComboScore = 0;
        }

        private void EvaluateDestructionFeats(float height, int mult)
        {
            // Skyscraper Demolition
            if (height >= 65f)
            {
                string towerTitle = height >= 85f ? "MÉGA-GRATTE-CIEL PULVÉRISÉ !" : "GRATTE-CIEL ANÉANTI !";
                TriggerBanner(towerTitle, $"+{height * 30f:N0} BONUS D'ALTITUDE !", new Color(1f, 0.5f, 0.1f));
                PlayHeavyImpact();
                return;
            }

            // Combo milestones
            if (currentCombo == 3)
            {
                TriggerBanner("TRIPLE RAVAGE !", "Multiplicateur x3 activé !", new Color(1f, 0.85f, 0.15f));
            }
            else if (currentCombo == 5)
            {
                TriggerBanner("FUREUR DRACONIQUE !", "Multiplicateur x4 en feu !", new Color(1f, 0.45f, 0.1f));
                PlayHeavyImpact();
            }
            else if (currentCombo == 8)
            {
                TriggerBanner("FLÉAU URBAIN !", "Multiplicateur x5 destructeur !", new Color(1f, 0.25f, 0.15f));
                PlayHeavyImpact();
            }
            else if (currentCombo == 12)
            {
                TriggerBanner("CATACLYSME !", "Multiplicateur x6 !", new Color(0.95f, 0.2f, 0.85f));
                PlayHeavyImpact();
            }
            else if (currentCombo == 18)
            {
                TriggerBanner("APOCALYPSE DRACONIQUE !", "MULTIPLICATEUR x8 MAXIMAL !", new Color(1f, 0.2f, 1f));
                PlayHeavyImpact();
            }
        }

        private void CheckRankProgression()
        {
            for (int i = RANKS.Length - 1; i >= 0; i--)
            {
                if (totalScore >= RANKS[i].threshold)
                {
                    if (i > currentRankIndex)
                    {
                        currentRankIndex = i;
                        TriggerBanner($"NOUVEAU RANG : {RANKS[i].title} !", $"Score supérieur à {RANKS[i].threshold:N0} pts", RANKS[i].col);
                        PlayHeavyImpact();
                        NotifyDragonRankEvolution(currentRankIndex);
                    }
                    break;
                }
            }
        }

        private void NotifyDragonRankEvolution(int rank)
        {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            var flight = FindAnyObjectByType<DragonFlightController>();
            var breath = FindAnyObjectByType<DragonBreath>();
#else
            var flight = FindObjectOfType<DragonFlightController>();
            var breath = FindObjectOfType<DragonBreath>();
#endif
            if (flight != null) flight.ApplyRankEvolution(rank);
            if (breath != null) breath.ApplyRankEvolution(rank);
        }

        private void AddScoreRaw(int pts)
        {
            totalScore += pts;
            if (totalScore > highScore)
            {
                highScore = totalScore;
                PlayerPrefs.SetInt("DragonCity_HighScore", highScore);
            }
        }

        public void TriggerBanner(string title, string subtext, Color col)
        {
            activeBannerText = title;
            activeBannerSubtext = subtext;
            bannerColor = col;
            bannerTimer = bannerDuration;
        }

        private void SpawnScorePopup(Vector3 worldPos, string text, Color col)
        {
            activePopups.Add(new ScorePopup
            {
                worldPosition = worldPos,
                text = text,
                color = col,
                timer = 1.6f,
                maxLifetime = 1.6f,
                scale = 1.3f
            });

            // Cap count to keep performance rock-solid
            if (activePopups.Count > 30)
            {
                activePopups.RemoveAt(0);
            }
        }

        // ==========================================
        // PROCEDURAL AUDIO SYNTHESIS
        // ==========================================
        private void SetupProceduralAudio()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D crisp UI audio

            chimeClip = GenerateChimeClip();
            heavyImpactClip = GenerateHeavyBoomClip();
        }

        private AudioClip GenerateChimeClip()
        {
            int sampleRate = 44100;
            float duration = 0.5f;
            int numSamples = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[numSamples];

            float baseFreq = 660f; // E5 crystal bell
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 9.5f);

                // Harmonic bell frequencies (fundamental + octave + minor third)
                float s1 = Mathf.Sin(2f * Mathf.PI * baseFreq * t);
                float s2 = Mathf.Sin(2f * Mathf.PI * (baseFreq * 2.02f) * t) * 0.45f;
                float s3 = Mathf.Sin(2f * Mathf.PI * (baseFreq * 3.14f) * t) * 0.22f;

                samples[i] = (s1 + s2 + s3) * env * 0.4f;
            }

            AudioClip clip = AudioClip.Create("ScoreChime", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateHeavyBoomClip()
        {
            int sampleRate = 44100;
            float duration = 1.2f;
            int numSamples = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 4.2f);
                float freq = Mathf.Lerp(140f, 38f, t / duration); // Pitch drop sweep
                float sine = Mathf.Sin(2f * Mathf.PI * freq * t);
                float noise = (Random.value * 2f - 1f) * 0.35f;

                samples[i] = (sine * 0.7f + noise) * env * 0.55f;
            }

            AudioClip clip = AudioClip.Create("HeavyBoom", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlayComboChime(int mult)
        {
            if (audioSource == null || chimeClip == null) return;
            // High combo scales the pitch higher and higher!
            audioSource.pitch = 1.0f + (mult - 1) * 0.12f + Random.Range(-0.03f, 0.03f);
            audioSource.PlayOneShot(chimeClip, 0.75f);
        }

        private void PlayHeavyImpact()
        {
            if (audioSource == null || heavyImpactClip == null) return;
            audioSource.pitch = Random.Range(0.92f, 1.05f);
            audioSource.PlayOneShot(heavyImpactClip, 0.85f);
        }

        // ==========================================
        // JUICY ARCADE ONGUI HUD
        // ==========================================
        private void OnGUI()
        {
            InitStyles();

            // 1. Top-Right Score & Stats Panel
            DrawScoreboardTopRight();

            // 2. Top-Center Combo Gauge & Multiplier
            if (comboTimer > 0f && currentCombo >= 2)
            {
                DrawComboCenter();
            }

            // 3. Floating World Popups (+1,250 x3)
            DrawWorldPopups();

            // 4. Center Feat Banner Notification
            if (bannerTimer > 0f)
            {
                DrawBanner();
            }

            // 5. End Game Victory Screen (if all buildings demolished)
            if (isGameFinished)
            {
                DrawVictoryModal();
            }
        }

        private void InitStyles()
        {
            if (whitePixel == null)
            {
                whitePixel = new Texture2D(1, 1);
                whitePixel.SetPixel(0, 0, Color.white);
                whitePixel.Apply();
            }

            if (scoreLabelStyle == null)
            {
                scoreLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 24,
                    fontStyle = FontStyle.Bold
                };

                smallLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };

                comboLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 32,
                    fontStyle = FontStyle.Bold
                };

                bannerTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 28,
                    fontStyle = FontStyle.Bold
                };

                bannerSubStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15,
                    fontStyle = FontStyle.Normal
                };

                popupStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 17,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        private void DrawScoreboardTopRight()
        {
            float panelWidth = 320f;
            float panelX = Screen.width - panelWidth - 20f;
            float panelY = 18f;

            // Translucent Dark Background Card
            GUI.color = new Color(0.06f, 0.07f, 0.09f, 0.76f);
            GUI.DrawTexture(new Rect(panelX - 10f, panelY - 8f, panelWidth + 18f, 122f), whitePixel);

            // Glowing Accent Border on the right
            GUI.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            GUI.DrawTexture(new Rect(panelX + panelWidth + 6f, panelY - 8f, 3f, 122f), whitePixel);

            // 1. Total Score (Large & punchy)
            scoreLabelStyle.fontSize = Mathf.RoundToInt(25 * scorePunchScale);
            string scoreStr = $"SCORE : {Mathf.RoundToInt(displayedScore):N0}";
            DrawOutlinedText(new Rect(panelX, panelY, panelWidth, 30f), scoreStr, scoreLabelStyle, new Color(1f, 0.92f, 0.35f), Color.black, 2);

            // 2. High Score
            string highStr = $"MEILLEUR : {highScore:N0}";
            DrawOutlinedText(new Rect(panelX, panelY + 28f, panelWidth, 20f), highStr, smallLabelStyle, new Color(0.85f, 0.85f, 0.85f, 0.8f), Color.black, 1);

            // 3. Buildings Destroyed & % of City
            float pct = (totalBuildings > 0) ? ((float)buildingsDestroyed / totalBuildings) * 100f : 0f;
            string destrStr = $"DÉTRUITS : {buildingsDestroyed} / {totalBuildings} ({pct:F1}%)";
            DrawOutlinedText(new Rect(panelX, panelY + 48f, panelWidth, 20f), destrStr, smallLabelStyle, new Color(1f, 0.45f, 0.3f), Color.black, 1);

            // 4. Speedrun Timer
            int m = Mathf.FloorToInt(gameTime / 60f);
            int s = Mathf.FloorToInt(gameTime % 60f);
            int f = Mathf.FloorToInt((gameTime * 10f) % 10f);
            string timeStr = isGameFinished ? $"TEMPS FINAL : {finalTimeFormatted}" : $"TEMPS : {m:00}:{s:00}.{f:0}";
            DrawOutlinedText(new Rect(panelX, panelY + 68f, panelWidth, 20f), timeStr, smallLabelStyle, new Color(0.35f, 0.95f, 1f), Color.black, 1);

            // 5. Current Rank
            var rank = RANKS[currentRankIndex];
            string rankStr = $"[ RANG : {rank.title.ToUpper()} ]";
            DrawOutlinedText(new Rect(panelX, panelY + 88f, panelWidth, 20f), rankStr, smallLabelStyle, rank.col, Color.black, 1);

            GUI.color = Color.white;
        }

        private void DrawComboCenter()
        {
            float cx = Screen.width * 0.5f;
            float topY = 22f;

            Color comboColor = GetComboColor(comboMultiplier);

            // Pulsing Combo Title
            comboLabelStyle.fontSize = Mathf.RoundToInt(30 * comboPunchScale);
            string comboText = $"COMBO x{comboMultiplier} !";
            DrawOutlinedText(new Rect(cx - 150f, topY, 300f, 36f), comboText, comboLabelStyle, comboColor, Color.black, 2);

            // Subtitle: Number of chained demolitions
            string subText = $"{currentCombo} BÂTIMENTS ENCHAÎNÉS";
            DrawOutlinedText(new Rect(cx - 150f, topY + 34f, 300f, 18f), subText, smallLabelStyle, Color.white, Color.black, 1);

            // Combo Decay Gauge Bar
            float barWidth = 220f;
            float barHeight = 8f;
            float barX = cx - (barWidth * 0.5f);
            float barY = topY + 56f;
            float fillRatio = Mathf.Clamp01(comboTimer / comboDuration);

            // Gauge background
            GUI.color = new Color(0.1f, 0.1f, 0.12f, 0.85f);
            GUI.DrawTexture(new Rect(barX - 2f, barY - 2f, barWidth + 4f, barHeight + 4f), whitePixel);

            // Gauge fill
            GUI.color = comboColor;
            GUI.DrawTexture(new Rect(barX, barY, barWidth * fillRatio, barHeight), whitePixel);

            GUI.color = Color.white;
        }

        private void DrawWorldPopups()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            foreach (var p in activePopups)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(p.worldPosition);
                // Only render if within front frustum
                if (screenPos.z > 0.8f)
                {
                    float alpha = Mathf.Clamp01(p.timer / 0.5f);
                    Color col = new Color(p.color.r, p.color.g, p.color.b, alpha);

                    // Unity GUI Y is inverted from screen coordinates
                    float sx = screenPos.x;
                    float sy = Screen.height - screenPos.y;

                    popupStyle.fontSize = Mathf.RoundToInt(17 * p.scale);
                    DrawOutlinedText(new Rect(sx - 120f, sy - 15f, 240f, 30f), p.text, popupStyle, col, new Color(0f, 0f, 0f, alpha), 2);
                }
            }
        }

        private void DrawBanner()
        {
            float cx = Screen.width * 0.5f;
            float bannerY = 105f;
            float bannerW = 560f;
            float bannerH = 70f;

            float alpha = Mathf.Clamp01(bannerTimer / 0.45f);

            // Dark Backdrop
            GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.88f * alpha);
            GUI.DrawTexture(new Rect(cx - (bannerW * 0.5f), bannerY, bannerW, bannerH), whitePixel);

            // Top & Bottom Gold/Fire accent bars
            GUI.color = new Color(bannerColor.r, bannerColor.g, bannerColor.b, 0.95f * alpha);
            GUI.DrawTexture(new Rect(cx - (bannerW * 0.5f), bannerY, bannerW, 2f), whitePixel);
            GUI.DrawTexture(new Rect(cx - (bannerW * 0.5f), bannerY + bannerH - 2f, bannerW, 2f), whitePixel);

            // Banner Title
            DrawOutlinedText(new Rect(cx - (bannerW * 0.5f), bannerY + 8f, bannerW, 32f), activeBannerText, bannerTitleStyle, 
                             new Color(bannerColor.r, bannerColor.g, bannerColor.b, alpha), new Color(0f, 0f, 0f, alpha), 2);

            // Banner Subtext
            DrawOutlinedText(new Rect(cx - (bannerW * 0.5f), bannerY + 40f, bannerW, 22f), activeBannerSubtext, bannerSubStyle,
                             new Color(0.95f, 0.95f, 0.95f, alpha), new Color(0f, 0f, 0f, alpha), 1);

            GUI.color = Color.white;
        }

        private void DrawVictoryModal()
        {
            float modalW = 660f;
            float modalH = 430f;
            float modalX = (Screen.width - modalW) * 0.5f;
            float modalY = (Screen.height - modalH) * 0.5f;

            // 1. Full-screen subtle dark backdrop
            GUI.color = new Color(0.02f, 0.02f, 0.04f, 0.78f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whitePixel);

            // 2. Central Modal Card
            GUI.color = new Color(0.06f, 0.07f, 0.10f, 0.95f);
            GUI.DrawTexture(new Rect(modalX, modalY, modalW, modalH), whitePixel);

            // 3. Glowing Radiant Border
            GUI.color = speedGradeColor;
            GUI.DrawTexture(new Rect(modalX, modalY, modalW, 4f), whitePixel);
            GUI.DrawTexture(new Rect(modalX, modalY + modalH - 4f, modalW, 4f), whitePixel);
            GUI.DrawTexture(new Rect(modalX, modalY, 4f, modalH), whitePixel);
            GUI.DrawTexture(new Rect(modalX + modalW - 4f, modalY, 4f, modalH), whitePixel);

            GUI.color = Color.white;

            // Title
            GUIStyle vTitleStyle = new GUIStyle(bannerTitleStyle)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold
            };
            DrawOutlinedText(new Rect(modalX, modalY + 16f, modalW, 34f), "🏆 VILLE 100% ANÉANTIE ! VICTOIRE TOTALE ! 🏆", vTitleStyle, new Color(1f, 0.88f, 0.25f), Color.black, 2);

            // Time Box
            GUIStyle vTimeLabelStyle = new GUIStyle(smallLabelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };
            DrawOutlinedText(new Rect(modalX, modalY + 54f, modalW, 18f), "CHRONOMÈTRE DE DESTRUCTION COMPLÈTE", vTimeLabelStyle, new Color(0.85f, 0.85f, 0.85f), Color.black, 1);

            GUIStyle vTimeValStyle = new GUIStyle(comboLabelStyle)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold
            };
            DrawOutlinedText(new Rect(modalX, modalY + 72f, modalW, 50f), finalTimeFormatted, vTimeValStyle, new Color(0.35f, 0.95f, 1f), Color.black, 3);

            // Speed Grade Banner
            GUIStyle vGradeStyle = new GUIStyle(comboLabelStyle)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            DrawOutlinedText(new Rect(modalX, modalY + 128f, modalW, 32f), $"{speedGrade} : {speedGradeTitle}", vGradeStyle, speedGradeColor, Color.black, 2);

            // Stats Grid
            float statsY = modalY + 168f;
            float statsW = modalW - 60f;
            float colX = modalX + 30f;

            GUI.color = new Color(0.12f, 0.13f, 0.18f, 0.85f);
            GUI.DrawTexture(new Rect(colX, statsY, statsW, 142f), whitePixel);
            GUI.color = Color.white;

            GUIStyle statKeyStyle = new GUIStyle(smallLabelStyle) { alignment = TextAnchor.MiddleLeft, fontSize = 14 };
            GUIStyle statValStyle = new GUIStyle(smallLabelStyle) { alignment = TextAnchor.MiddleRight, fontSize = 14, fontStyle = FontStyle.Bold };

            // Row 1: Score
            DrawOutlinedText(new Rect(colX + 15f, statsY + 10f, statsW * 0.5f, 22f), "SCORE DE CARNAGE :", statKeyStyle, Color.white, Color.black, 1);
            DrawOutlinedText(new Rect(colX + statsW * 0.5f, statsY + 10f, statsW * 0.5f - 15f, 22f), $"{totalScore:N0} PTS", statValStyle, new Color(1f, 0.92f, 0.35f), Color.black, 1);

            // Row 2: Bâtiments
            DrawOutlinedText(new Rect(colX + 15f, statsY + 38f, statsW * 0.5f, 22f), "BÂTIMENTS PULVÉRISÉS :", statKeyStyle, Color.white, Color.black, 1);
            DrawOutlinedText(new Rect(colX + statsW * 0.5f, statsY + 38f, statsW * 0.5f - 15f, 22f), $"{buildingsDestroyed} / {totalBuildings} (100%)", statValStyle, new Color(1f, 0.45f, 0.25f), Color.black, 1);

            // Row 3: Combo
            DrawOutlinedText(new Rect(colX + 15f, statsY + 66f, statsW * 0.5f, 22f), "MEILLEUR COMBO RÉALISÉ :", statKeyStyle, Color.white, Color.black, 1);
            DrawOutlinedText(new Rect(colX + statsW * 0.5f, statsY + 66f, statsW * 0.5f - 15f, 22f), $"x{maxComboAchieved} !", statValStyle, new Color(0.25f, 0.95f, 0.45f), Color.black, 1);

            // Row 4: Dragon Rank
            var finalRank = RANKS[currentRankIndex];
            DrawOutlinedText(new Rect(colX + 15f, statsY + 94f, statsW * 0.5f, 22f), "RANG FINAL DU DRAGON :", statKeyStyle, Color.white, Color.black, 1);
            DrawOutlinedText(new Rect(colX + statsW * 0.5f, statsY + 94f, statsW * 0.5f - 15f, 22f), $"{finalRank.title.ToUpper()}", statValStyle, finalRank.col, Color.black, 1);

            // Restart Button at Bottom
            float btnW = 400f;
            float btnH = 44f;
            float btnX = modalX + (modalW - btnW) * 0.5f;
            float btnY = modalY + modalH - 58f;

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            btnStyle.normal.textColor = Color.white;

            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "🔄 [ R ] RECONSTRUIRE LA VILLE & REJOUER", btnStyle))
            {
                RestartCity();
            }
        }

        private void DrawOutlinedText(Rect rect, string text, GUIStyle style, Color textColor, Color outlineColor, int outlineDist)
        {
            Color oldCol = style.normal.textColor;
            style.normal.textColor = outlineColor;

            for (int x = -outlineDist; x <= outlineDist; x += outlineDist)
            {
                for (int y = -outlineDist; y <= outlineDist; y += outlineDist)
                {
                    if (x == 0 && y == 0) continue;
                    GUI.Label(new Rect(rect.x + x, rect.y + y, rect.width, rect.height), text, style);
                }
            }

            style.normal.textColor = textColor;
            GUI.Label(rect, text, style);
            style.normal.textColor = oldCol;
        }
    }
}
