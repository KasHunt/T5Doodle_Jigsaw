using System;
using System.Collections;
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
		public GameObject modalBackground;
		public AudioClip uiNavSound;
		
		[Header("HUD Icons")]
		public GameObject saveIcon;
		public GameObject settingMenu;
		public GameObject jigsawMenu;

		[Header("Settings Pages")]
		public GameObject settingsPage;
		public GameObject newJigsawPage;

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

		public void ShowSettingsScreen()
		{
			PlayUiNavSound();
			settingMenu.SetActive(false);
			
			modalBackground.SetActive(true);
			settingsPage.SetActive(true);
		}
		
		public void HideSettingsScreen()
		{
			PlayUiNavSound();
			modalBackground.SetActive(false);
			settingsPage.SetActive(false);
		}
		
		public void ShowNewJigsawScreen()
		{
			PlayUiNavSound();
			settingMenu.SetActive(false);
			
			modalBackground.SetActive(true);
			newJigsawPage.SetActive(true);
		}
		
		public void HideNewJigsawScreen()
		{
			PlayUiNavSound();
			modalBackground.SetActive(false);
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

			// Coroutine example
			StartCoroutine(ShowLoadImageDialogCoroutine(LoadMode.Jigsaw));			
		}
		
		public void LoadImage()
		{
			PlayUiNavSound();
			HideNewJigsawScreen();
			
			FileBrowser.SetFilters(true, 
				new FileBrowser.Filter("Images", ".jpg", ".jpeg", ".png", ".gif"));
			FileBrowser.SetDefaultFilter(".jpg");
			FileBrowser.SetExcludedExtensions(".lnk", ".tmp", ".zip", ".rar", ".exe");

			// Coroutine example
			StartCoroutine(ShowLoadImageDialogCoroutine(LoadMode.Image));			
		}
		
		public void SetSaveIconVisible(bool visible)
		{
			saveIcon.SetActive(visible);
		}

		private IEnumerator ShowLoadImageDialogCoroutine(LoadMode mode)
		{
			// Show the modal background
			modalBackground.SetActive(true);
			
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
			
			yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.FilesAndFolders, false, initialPath, null,
				title, "Load");

			// Clear the modal background
			modalBackground.SetActive(false);
			
			if (!FileBrowser.Success)
			{
				yield break;
			}

			// Find the Jigsaw
			var jigsaw = Jigsaw.Instance;
			if (!jigsaw)
			{
				Log.Warn("Failed to find Jigsaw Object");
				yield break;
			}

			switch (mode)
			{
				case LoadMode.Jigsaw:
					Log.Info("Loading image for Jigsaw : " + FileBrowser.Result[0]);
					jigsaw.LoadJigsaw(FileBrowser.Result[0]);
					break;
				
				case LoadMode.Image:
					Log.Info("Loading image for Jigsaw : " + FileBrowser.Result[0]);
					jigsaw.LoadImage(FileBrowser.Result[0], _newJigsawRows, _newJigsawCols);
					break;
				
				default:
					throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
			}
		}
	}
}