using System;
using SimpleFileBrowser;
using TiltFive.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Scripts
{

	public class JigsawUI : MonoBehaviour
	{
		private enum LoadMode
		{
			Jigsaw,
			Image,
		}

		[Header("Common")]
		public AudioClip uiNavSound;
		
		[Header("HUD Icons")]
		public GameObject settingMenu;
		public GameObject jigsawMenu;

		[Header("Pages")]
		public GameObject settingsPage;
		public GameObject newJigsawPage;
		public GameObject helpPage;

		[Header("New Jigsaw Page UI Elements")]
		public GameObject newRowsText;
		public GameObject newColsText;
		public GameObject newRowsSlider;
		public GameObject newColsSlider;
		
		[Header("Setting Page UI Elements")]
		public GameObject soundEffectVolumeSlider;
		public GameObject musicVolumeSlider;
		public GameObject wandArcSlider;

		[Header("Jigsaw Size Limits")]
		private int _newJigsawRows = 5;
		private int _newJigsawCols = 5;
		
		private TextMeshProUGUI _rowsTextMeshPro;
		private TextMeshProUGUI _colsTextMeshPro;
		
		private Slider _rowsSlider;
		private Slider _colsSlider;
		private Slider _soundEffectVolumeSlider;
		private Slider _musicVolumeSlider;
		private Slider _wandArcSlider;
        
		
		// Static instance of JigsawUI, accessible from anywhere
		public static JigsawUI Instance { get; private set; }

		private void Awake()
		{
			// Check for existing instances and destroy this instance if we've already got one
			if (Instance != null && Instance != this)
			{
				Log.Warn("Destroying duplicate JigsawUI");
				Destroy(gameObject);
				return;
			}

			// Set this instance as the Singleton instance
			Instance = this;
            
			// Persist across scenes
			DontDestroyOnLoad(gameObject);
		}

		private void Start()
		{
			// Get new jigsaw page UI element handles
			_rowsTextMeshPro = newRowsText.GetComponent<TextMeshProUGUI>();
			_colsTextMeshPro = newColsText.GetComponent<TextMeshProUGUI>();
			_rowsSlider = newRowsSlider.GetComponent<Slider>();
			_colsSlider = newColsSlider.GetComponent<Slider>();
			_soundEffectVolumeSlider = soundEffectVolumeSlider.GetComponent<Slider>();
			_musicVolumeSlider = musicVolumeSlider.GetComponent<Slider>();
			_wandArcSlider = wandArcSlider.GetComponent<Slider>();
			
			// Set the initial slider values
			_rowsTextMeshPro.text = _newJigsawRows.ToString();
			_colsTextMeshPro.text = _newJigsawCols.ToString();
			_rowsSlider.minValue = Jigsaw.JigsawDimensionMin;
			_rowsSlider.maxValue = Jigsaw.JigsawDimensionMax;
			_colsSlider.minValue = Jigsaw.JigsawDimensionMin;
			_colsSlider.maxValue = Jigsaw.JigsawDimensionMax;
			_rowsSlider.value = _newJigsawRows;
			_colsSlider.value = _newJigsawCols;
			
			_soundEffectVolumeSlider.value = SoundManager.Instance.EffectVolume;
			_musicVolumeSlider.value = SoundManager.Instance.MusicVolume;
			_wandArcSlider.value = WandManager.Instance.arcLaunchVelocity;
		}

		private void PlayUiNavSound()
		{
			SoundManager.Instance.PlaySound(uiNavSound, 1);
		}
		
		public void SetNewJigsawRows(float newRows)
		{
			_rowsTextMeshPro.text = ((int)newRows).ToString();
			_newJigsawRows = (int)newRows;
		}
        
		public void SetNewJigsawCols(float newCols)
		{
			_colsTextMeshPro.text = ((int)newCols).ToString();
			_newJigsawCols = (int)newCols;
		}

		public void SetMusicVolume(float value)
		{
			SoundManager.Instance.MusicVolume = value;
		}
		
		public void SetSoundEffectsVolume(float value)
		{
			SoundManager.Instance.EffectVolume = value;
		}
		
		public void SetWandArcVelocity(float value)
		{
			WandManager.Instance.arcLaunchVelocity = value;
		}
		
		public void JumbleJigsaw()
		{
			PlayUiNavSound();
			jigsawMenu.SetActive(false);
			if (Jigsaw.Instance) Jigsaw.Instance.Jumble();
		}
		
		public void ResetJigsaw()
		{
			PlayUiNavSound();
			jigsawMenu.SetActive(false);
			if (Jigsaw.Instance) Jigsaw.Instance.Reset();
		}
		
		public void ToggleHint()
		{
			PlayUiNavSound();
			jigsawMenu.SetActive(false);
			if (Jigsaw.Instance) Jigsaw.Instance.SetHintVisible(!Jigsaw.Instance.IsHintVisible());
		}

		public void ToggleSettingsMenu()
		{
			PlayUiNavSound();
			settingMenu.SetActive(!settingMenu.activeSelf);
			jigsawMenu.SetActive(false);
		}
		
		public void TogglePuzzleMenu()
		{
			PlayUiNavSound();
			jigsawMenu.SetActive(!jigsawMenu.activeSelf);
			settingMenu.SetActive(false);
		}

		public void ToggleHelpScreen()
		{
			PlayUiNavSound();
			helpPage.SetActive(!helpPage.activeSelf);
		}
		
		public void ShowSettingsScreen()
		{
			PlayUiNavSound();
			settingMenu.SetActive(false);
			settingsPage.SetActive(true);
		}
		
		public void HideSettingsScreen()
		{
			PlayUiNavSound();
			settingsPage.SetActive(false);
		}
		
		public void ShowNewJigsawScreen()
		{
			PlayUiNavSound();
			settingMenu.SetActive(false);
			newJigsawPage.SetActive(true);
		}
		
		public void HideNewJigsawScreen()
		{
			PlayUiNavSound();
			newJigsawPage.SetActive(false);
		}
		
		public void Quit()
		{
			PlayUiNavSound();
#if UNITY_EDITOR
			UnityEditor.EditorApplication.ExitPlaymode();
#else
	        Application.Quit();
#endif
		}
		
		public void LoadJigsaw()
		{
			PlayUiNavSound();
			settingMenu.SetActive(false);
			
			FileBrowser.SetFilters(true, 
				new FileBrowser.Filter("Jigsaw Saves", ".jig"));
			FileBrowser.SetDefaultFilter(".jig");
			FileBrowser.SetExcludedExtensions(".lnk", ".tmp", ".zip", ".rar", ".exe");

			// State the coroutine based load dialog
			ShowLoadImageDialog(LoadMode.Jigsaw);			
		}
		
		public void LoadImage()
		{
			PlayUiNavSound();
			HideNewJigsawScreen();
			
			FileBrowser.SetFilters(true, 
				new FileBrowser.Filter("Images", ".jpg", ".jpeg", ".png", ".gif"));
			FileBrowser.SetDefaultFilter(".jpg");
			FileBrowser.SetExcludedExtensions(".lnk", ".tmp", ".zip", ".rar", ".exe");

			// State the coroutine based load dialog
			ShowLoadImageDialog(LoadMode.Image);			
		}

		private static void PopUI()
		{
			GameboardCanvas.Instance.PopCanvas();			
		}
		
		private static void OnPickJigsawFile(string[] paths)
		{
			PopUI();
			
			// Find the Jigsaw
			var jigsaw = Jigsaw.Instance;
			if (!jigsaw)
			{
				Log.Warn("Failed to find Jigsaw Object");
				return;
			}

			Log.Info("Loading image for Jigsaw : " + paths[0]);
			jigsaw.LoadJigsaw(paths[0]);
		}
		
		private void OnPickImageFile(string[] paths)
		{
			PopUI();
			
			// Find the Jigsaw
			var jigsaw = Jigsaw.Instance;
			if (!jigsaw)
			{
				Log.Warn("Failed to find Jigsaw Object");
				return;
			}

			Log.Info("Loading image for Jigsaw : " + FileBrowser.Result[0]);
			jigsaw.LoadImage(paths[0], _newJigsawRows, _newJigsawCols);
		}
		
		private void ShowLoadImageDialog(LoadMode mode)
		{
			var title = mode switch
			{
				LoadMode.Jigsaw => "Select Jigsaw",
				LoadMode.Image => "Select Image",
				_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
			};

			var initialPath = mode switch
			{
				LoadMode.Jigsaw => Application.persistentDataPath,
				LoadMode.Image => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
				_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
			};
            
			var fileBrowserGameObject = FindObjectOfType<FileBrowser>(true);
			var gameboardCanvas = fileBrowserGameObject.gameObject.GetComponent<Canvas>();
			GameboardCanvas.Instance.PushCanvas(gameboardCanvas);
			fileBrowserGameObject.gameObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(300, 300);
			
			FileBrowser.ShowLoadDialog(
				mode switch
				{
					LoadMode.Jigsaw => OnPickJigsawFile,
					LoadMode.Image => OnPickImageFile,
					_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
				},
				PopUI,
				FileBrowser.PickMode.Files,
				false,
				initialPath,
				null,
				title,
				"Load");
		}
	}
}