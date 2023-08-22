using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TiltFive;

namespace TiltFive
{
    [System.Serializable]
    public class PlayerSettings
    {
        #region Sub-settings

        public GlassesSettings glassesSettings = new GlassesSettings();

        public ScaleSettings scaleSettings = new ScaleSettings();

        public GameBoardSettings gameboardSettings = new GameBoardSettings();

        public WandSettings leftWandSettings = new WandSettings();
        public WandSettings rightWandSettings = new WandSettings();

        #endregion


        #region Public Properties

        public PlayerIndex PlayerIndex;

        public static uint MAX_SUPPORTED_PLAYERS => GlassesSettings.MAX_SUPPORTED_GLASSES_COUNT;

        #endregion


        #region Public Functions

        public void Validate()
        {
            rightWandSettings.controllerIndex = ControllerIndex.Right;
            leftWandSettings.controllerIndex = ControllerIndex.Left;
        }

        #endregion
    }
}