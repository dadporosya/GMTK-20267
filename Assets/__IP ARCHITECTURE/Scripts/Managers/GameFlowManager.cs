using System.Collections.Generic;
using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance; // Instance

    public enum State
    {
        Intro,
        Game,
        Pause,
        Loss,
        Finale
    }
    
    private List<State> onPauseStates = new List<State>()
    {
        State.Pause, State.Loss, State.Intro
    };

    private List<State> onGameStates = new List<State>()
    {
        State.Game
    };
    
    public State state = State.Game;
    
    public void SetOnPause()
    {
        state = State.Pause;
        h.Out("STATE PAUSE");
    }

    public void SetOnGame() 
    {
        state = State.Game;
        h.Out("STATE GAME");
    }

    public bool IsGame()
    {
        return onGameStates.Contains(state);
    }
    public bool IsPaused()
    {
        return onPauseStates.Contains(state);
    }
    
    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);
    }
    
    
    
}
