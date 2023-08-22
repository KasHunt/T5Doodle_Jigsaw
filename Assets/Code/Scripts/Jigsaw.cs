using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using TiltFive.Logging;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Code.Scripts
{
    public static class SettingsManager
    {
        [Serializable]
        public struct Settings
        {
            public float wandArcVelocity;
            public float musicVolume;
            public float effectVolume;
            public string lastLoadedJigsaw;
        }
        
        private const string FileName = "settings.json";

        public static void Save(Settings settings)
        {
            var json = JsonUtility.ToJson(settings);
            var path = Path.Combine(Application.persistentDataPath, FileName);
            File.WriteAllText(path, json);
        }

        public static Settings Load()
        {
            var path = Path.Combine(Application.persistentDataPath, FileName);

            if (!File.Exists(path))
            {
                return new Settings()
                {
                    effectVolume = SoundManager.Instance.EffectVolume,
                    musicVolume = SoundManager.Instance.MusicVolume,
                    wandArcVelocity = WandManager.Instance.arcLaunchVelocity,
                    lastLoadedJigsaw = "",
                };   
            }
            
            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<Settings>(json);
        }
    }
    
    public readonly struct RowCol
    {
        private int Row { get; }
        private int Col { get; }

        public RowCol(int row, int col)
        {
            Row = row;
            Col = col;
        }

        // You may need to override Equals and GetHashCode as well
        public override bool Equals(object obj)
        {
            if (obj is RowCol other)
            {
                return Row == other.Row && Col == other.Col;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Row.GetHashCode() ^ Col.GetHashCode();
        }
    }
    
    [Serializable]
    public class SaveData
    {
        public List<Piece> pieces = new();
        public byte[] imageData;
        public int rows;
        public int cols;
        public string savePath;
    }
    
    [Serializable]
    public class Piece
    {
        public int column;
        public int row;
        
        [Serializable]
        public class SerializableVector3
        {
            public float x;
            public float y;
            public float z;

            public SerializableVector3(Vector3 vec)
            {
                x = vec.x;
                y = vec.y;
                z = vec.z;
            }

            public Vector3 AsVector3()
            {
                return new Vector3(x, y, z);
            }
        }
        
        [Serializable]
        public class SerializableQuat
        {
            public float x;
            public float y;
            public float z;
            public float w;
            
            public SerializableQuat(Quaternion quat)
            {
                x = quat.x;
                y = quat.y;
                z = quat.z;
                w = quat.w;
            }
            
            public Quaternion AsQuaternion()
            {
                return new Quaternion(x, y, z, w);
            }
        }
        
        public enum Notch
        {
            In = -1,
            Flat = 0,
            Out = 1,
        }
        
        [Serializable]
        public class Notches
        {
            public Notch top = Notch.Flat;
            public Notch left = Notch.Flat;
            public Notch bottom = Notch.Flat;
            public Notch right = Notch.Flat;
        }

        public Notches notches;

        public bool storedPosition;
        public SerializableVector3 position;
        public SerializableQuat rotation;
        
        [NonSerialized]
        public GameObject PieceGameObject;
        [NonSerialized]
        public Collider PieceGameObjectCollider;
        [NonSerialized]
        public JigsawPiece JigsawPieceBehavior;
    }
    
    public class Jigsaw : MonoBehaviour
    {
        public GameObject pieceObject;
        
        [Header("Materials")]
        public Material material;
        public Material selectedMaterial;
        public Material hintMaterial;
        
        [Header("Timings")]
        [Range(0.1f, 10f)]
        public float flipAnimateTime = 0.5f;
        [Range(0.1f, 10f)]
        public float minAnimateTime = 0.5f;
        [Range(0.1f, 10f)]
        public float maxAnimateTime = 2.0f;
        
        [Header("Dimensions")]
        public float gridSize = 0.588f;
        public float pieceMoveHeight = 0.2f;
        [Range(0.5f, 12f)]
        public float jumbleDistance = 12f;
        [Range(2f, 6f)]
        public float jumbleLimit = 6f;
        
        [Header("Selector")]
        [Range(0.01f, 3.0f)]
        public float snapDistance = 0.7f;
        public float selectorScaleStep = 0.02f;
        public float selectorMinScale = 0.1f;
        public float selectorMaxScale = 5.0f;
        public float selectorHeight = 0.02f;
        
        [Header("Misc")]
        [Range(0, 1)]
        public float hintAlpha = 0.25f;
        [Range(2, 120)]
        public float saveIntervalInSeconds = 10;
        
        public const int JigsawDimensionMin = 2;
        public const int JigsawDimensionMax = 30;
        
        private SaveData _saveData = new();

        public List<Piece> AllPieces => _saveData.pieces;

        private GameObject _hintObject;
        private MeshRenderer _hintObjectMeshRenderer;
        
        private string _lastLoadedJigsaw = "";

        private Vector3 _gridOffset = Vector3.zero;
        
        private void Start()
        {
            StartCoroutine(SavePeriodically());
        }

        public static Jigsaw Instance { get; private set; }

        private void Awake()
        {
            // Check for existing instances and destroy this instance if we've already got one
            if (Instance != null && Instance != this)
            {
                Log.Warn("Destroying duplicate Jigsaw");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _hintObject = new GameObject("Hint");
            
            var settings = SettingsManager.Load();
            SoundManager.Instance.MusicVolume = settings.musicVolume;
            SoundManager.Instance.EffectVolume = settings.effectVolume;
            WandManager.Instance.arcLaunchVelocity = settings.wandArcVelocity;
            
            LoadJigsaw(settings.lastLoadedJigsaw);
            
            // Persist across scenes
            DontDestroyOnLoad(gameObject);
        }
        
        private void OnApplicationQuit()
        {
            SettingsManager.Save(new SettingsManager.Settings
            {
                musicVolume = SoundManager.Instance.MusicVolume,
                effectVolume = SoundManager.Instance.EffectVolume,
                wandArcVelocity = WandManager.Instance.arcLaunchVelocity,
                lastLoadedJigsaw = _lastLoadedJigsaw, 
            });
            
            if (!SavePieces())
            {
                Log.Warn("Failed to save");
            }
        }

        private IEnumerator SavePeriodically()
        {
            var wait = new WaitForSeconds(saveIntervalInSeconds);
            while (true)
            {
                // Don't attempt to save if we're not loaded
                if (_saveData.pieces.Count == 0)
                {
                    yield return wait;
                }
                
                if (!SavePieces())
                {
                    Log.Warn("Failed to save");
                }

                if (this.IsDestroyed())
                {
                    yield break;
                }
                
                yield return wait;
            }
        }
        
        private bool SavePieces()
        {
            if (JigsawUI.Instance) JigsawUI.Instance.SetSaveIconVisible(true);
            
            try
            {
                // Don't save if we don't have a filename to save to...
                var saveFilePath = _saveData.savePath; 
                if (saveFilePath == "")
                {
                    return false;
                }
            
                // Don't save if we've got no columns
                if (_saveData.pieces.Count == 0)
                {
                    return false;
                }
                
                // Transfer position and rotation from the GameObject (which isn't serialized)
                foreach (var piece in _saveData.pieces)
                {
                    piece.storedPosition = true;
                    piece.position = new Piece.SerializableVector3(piece.PieceGameObject.transform.position);
                    piece.rotation = new Piece.SerializableQuat(piece.PieceGameObject.transform.rotation);
                }

                var binaryFormatter = new BinaryFormatter();
                var file = new FileStream(saveFilePath, FileMode.Create);
                binaryFormatter.Serialize(file, _saveData);
                file.Close();
            }
            finally
            {
                if (JigsawUI.Instance) JigsawUI.Instance.SetSaveIconVisible(false);
            }
            return true;
        }
        
        private bool LoadPieces(string loadPath)
        {
            if (!File.Exists(loadPath))
            {
                return false;
            }

            var binaryFormatter = new BinaryFormatter();
            var file = new FileStream(loadPath, FileMode.Open);
            try
            {
                _saveData = (SaveData)binaryFormatter.Deserialize(file);
            }
            catch (Exception exception)
            {
                Log.Error("Failed to read saved data : " + loadPath + " (" + exception + ")");
                return false;
            }
            
            SetImageData(_saveData.imageData);

            _saveData.savePath = loadPath;
            
            return true;
        }

        public void LoadJigsaw(string loadPath)
        {
            // Save and clear any existing pieces
            SavePieces();
            ClearExisting();
            
            // Load the new pieces
            if (!LoadPieces(loadPath)) return;

            // Store the last loaded jigsaw (for fast reload)
            _lastLoadedJigsaw = loadPath;
            
            // Instantiate dependant GameObjects
            InstantiatePieces();
            InstantiateHint();
        }

        public void LoadImage(string path, int newJigsawRows, int newJigsawCols)
        {
            // Save and clear any existing pieces
            SavePieces();
            ClearExisting();
            
            // Generate new pieces and set the texture
            GeneratePieces(newJigsawRows, newJigsawCols);
            LoadImageTexture(path);
            
            // Instantiate dependant GameObjects
            InstantiatePieces();
            InstantiateHint();
            
            // Save the newly generated jigsaw
            SavePieces();
            
            // Jumble the initial jigsaw
            Jumble();
        }

        private static string GetUniqueSaveFilePath(string imageFilePath)
        {
            const string extension = ".jig";
            var baseFileName = Path.GetFileNameWithoutExtension(imageFilePath);
            var filename = baseFileName + extension;
            var counter = 1;

            // Check if the filename already ends with '_#'
            var regex = new Regex("_(\\d+)$");
            var match = regex.Match(baseFileName);
            if (match.Success)
            {
                var number = match.Groups[1].Value;
                baseFileName = baseFileName[..^match.Length];
                counter = int.Parse(number) + 1;
            }

            // Append "_#" to the filename if it already exists, incrementing the number as needed
            var path = Path.Combine(Application.persistentDataPath, filename);
            while (File.Exists(path))
            {
                filename = baseFileName + "_" + counter + extension;
                path = Path.Combine(Application.persistentDataPath, filename);
                counter++;
            }

            return path;
        }
        
        private void LoadImageTexture(string imagePath)
        {
            // Load the image file data
            byte[] fileData;
            try
            {
                fileData = File.ReadAllBytes(imagePath);
            }
            catch (Exception exception)
            {
                Log.Error("Failed to read image file : " + imagePath + " (" + exception + ")");
                return;
            }
            
            _saveData.savePath = GetUniqueSaveFilePath(imagePath);
            _saveData.imageData = fileData;
            _lastLoadedJigsaw = _saveData.savePath;

            if (!SetImageData(fileData))
            {
                Log.Error("Failed to create texture from image data : " + _saveData.savePath);
            }
        }

        private bool SetImageData(byte[] data)
        {
            // Create the texture from the data
            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!texture.LoadImage(data))
            {
                return false;
            }

            material.mainTexture = texture;
            selectedMaterial.mainTexture = texture;
            return true;
        }

        private void InstantiateHint()
        {
            // Create the hint object if it doesn't exist
            if (!_hintObjectMeshRenderer)
            {
                // Add the cube
                var meshFilter = _hintObject.AddComponent<MeshFilter>();
                meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                
                // Set the basic material properties
                var meshRenderer = _hintObject.AddComponent<MeshRenderer>();
                meshRenderer.material = hintMaterial;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.material.color = new Color(1, 1, 1, hintAlpha);
                _hintObjectMeshRenderer = meshRenderer;

                _hintObject.SetActive(false);
            }
            
            // Scale the hint object to match the puzzle
            _hintObject.transform.localScale =
                new Vector3(-gridSize * _saveData.rows, 0.03f, -gridSize * _saveData.cols);
            
            // Set the texture
            _hintObjectMeshRenderer.material.mainTexture = material.mainTexture;
        }

        public void SetHintVisible(bool shown)
        {
            _hintObject.SetActive(shown);
        }

        public bool IsHintVisible()
        {
            return _hintObject.activeSelf;
        }

        private Vector3 ComputeInitialPosition(Piece piece)
        {
            return new Vector3(piece.row * gridSize, 0, -piece.column * gridSize) + _gridOffset;
        }
        
        private void InstantiatePiece(Piece piece)
        {
            Vector3 position;
            Quaternion rotation;

            // Set the position of the piece
            if (piece.storedPosition)
            {
                // Use the stored position if we have it...
                position = piece.position.AsVector3();
                rotation = piece.rotation.AsQuaternion();
            }
            else
            {
                // ...otherwise generate the position
                position = ComputeInitialPosition(piece);
                rotation = Quaternion.identity;
            }
            
            var colSize = (1f / _saveData.cols);
            var rowSize = (1f / _saveData.rows);
            
            var pieceObjectInstance = Instantiate(pieceObject, position, rotation);
            if (!pieceObjectInstance)
            {
                Log.Warn("Piece Object not specified");
                return;
            }
            
            var pieceObjectBehavior = pieceObjectInstance.GetComponent<JigsawPiece>();
            if (!pieceObjectBehavior)
            {
                Log.Warn("Piece Object missing Piece Object behaviour");
                return;
            }
            pieceObjectBehavior.Setup(material, selectedMaterial, piece, colSize, rowSize);
            
            // Parent the new piece to the Jigsaw
            pieceObjectInstance.transform.SetParent(transform);
            
            // Store the GameObject in the piece
            piece.PieceGameObject = pieceObjectInstance;
            piece.PieceGameObjectCollider = pieceObjectInstance.GetComponent<Collider>();
            piece.JigsawPieceBehavior = pieceObjectBehavior;
        }

        public void Reset()
        {
            Debug.Log("Resetting Pieces");
            foreach (var piece in _saveData.pieces)
            {
                var target = ComputeInitialPosition(piece);
                piece.JigsawPieceBehavior.AnimateTo(target, 
                    Quaternion.identity, Random.Range(minAnimateTime, maxAnimateTime));
            }
        }
        
        public void Jumble()
        {
            Debug.Log("Jumbling Pieces");
            foreach (var piece in _saveData.pieces)
            {
                var target = new Vector3(
                    Mathf.Clamp(Random.Range(-jumbleDistance, jumbleDistance), -jumbleLimit, jumbleLimit),
                    0,
                    Mathf.Clamp(Random.Range(-jumbleDistance, jumbleDistance), -jumbleLimit, jumbleLimit)
                );
                piece.JigsawPieceBehavior.AnimateTo(target, 
                    Random.rotation, Random.Range(minAnimateTime, maxAnimateTime));
            }
        }

        private void ClearExisting()
        {
            // Destroy all existing piece GameObjects
            foreach (var piece in _saveData.pieces)
            {
                Destroy(piece.PieceGameObject);
            }

            // Clear the saved data
            _saveData = new SaveData();
        }
        
        private void InstantiatePieces()
        {
            _gridOffset = -new Vector3(((_saveData.rows / 2f - 0.5f) * gridSize), 0, -(_saveData.cols / 2f - 0.5f) * gridSize);
            
            foreach (var piece in _saveData.pieces)
            {
                InstantiatePiece(piece);
            }
        }
        
        private void GeneratePieces(int rows, int cols)
        {
            var notchesMap = new Dictionary<RowCol, Piece.Notches>();
            
            _saveData.cols = cols;
            _saveData.rows = rows;
            
            for (var col = 0; col < cols; col++)
            {
                for (var row = 0; row < rows; row++)
                {
                    var notches = new Piece.Notches
                    {
                        top = (row == 0)
                            ? Piece.Notch.Flat
                            : (Piece.Notch)(-1 * (int)notchesMap[new RowCol(row - 1, col)].bottom),
                        left = (col == 0)
                            ? Piece.Notch.Flat
                            : (Piece.Notch)(-1 * (int)notchesMap[new RowCol(row, col - 1)].right),
                        bottom = (row == (rows - 1))
                            ? Piece.Notch.Flat
                            : ((Random.Range(0, 2) == 0) ? Piece.Notch.In : Piece.Notch.Out),
                        right = (col == (cols - 1))
                            ? Piece.Notch.Flat
                            : ((Random.Range(0, 2) == 0) ? Piece.Notch.In : Piece.Notch.Out),
                    };

                    notchesMap[new RowCol(row, col)] = notches;
                    _saveData.pieces.Add(new Piece
                    {
                        row = row,
                        column = col,
                        notches = notches,
                    });
                }
            }
        }
    }
}
