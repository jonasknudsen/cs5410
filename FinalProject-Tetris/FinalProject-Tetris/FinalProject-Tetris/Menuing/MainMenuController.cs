using System.Collections.Generic;
using FinalProject_Tetris.InputHandling;
using FinalProject_Tetris.Menuing;
using static FinalProject_Tetris.Menuing.MenuState;

namespace FinalProject_Tetris
{
    public class MainMenuController : MenuController
    {
        // holds the state of this menu
        public MenuState MenuState;

        // holds a list of all high scores
        public List<int> HighScoresIntList;

        public MainMenuController(TetrisGameController controller)
        {
            GameController = controller;
        }

        public override void OpenMenu()
        {
            GameController.GameState = GameState.MainMenu;
            MenuState = Main;
        }

        public override void ProcessMenu(InputHandler inputHandler)
        {
            switch (MenuState)
            {
                case Main:
                    if (inputHandler.NewGameButton.Pressed)
                    {
                        GameController.StartGame(false);
                    }
                    else if (inputHandler.HighScoresButton.Pressed)
                    {
                        MenuState = HighScores;
                    }
                    else if (inputHandler.CustomizeControlsButton.Pressed)
                    {
                        MenuState = Controls;
                    }
                    else if (inputHandler.ViewCreditsButton.Pressed)
                    {
                        MenuState = Credits;
                    }
                    break;
                case HighScores:
                case Credits:
                    if (inputHandler.BackToMainButton.Pressed)
                        MenuState = Main;
                    break;
                default:
                    break;
            }
        }
    }
}