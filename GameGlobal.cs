using System.Collections.Generic;
using Godot;
using Godot.Collections;

public partial class GameGlobal : Node
{
    public readonly string UserDataFilePath = "user://game.tres";

    public Camera2D Camera2D { get; set; }
    public Stat Stat { get; set; }

    public Godot.Collections.Dictionary<string, EnemyAliveState> WorldState =
        new Godot.Collections.Dictionary<string, EnemyAliveState>();

    public ColorRect ColorRect { get; set; }
    
    public override void _Ready() {
        Stat = GetNode<Stat>("Stat");
        ColorRect = GetNode<ColorRect>("SceneFade/ColorRect");
        ColorRect.Color = new Color(ColorRect.Color,0.0f);
    }

    public void NewGame()
    {
        GetTree().ChangeSceneToFile("res://world.tscn");
    }

    public async void ChangeScene(string path, string entryPointName)
    {
        var tween = GetTree().CreateTween();
        tween.TweenProperty(ColorRect, "color:a", 1, 0.2);
        await ToSignal(tween, Tween.SignalName.Finished);
        GetTree().ChangeSceneToFile(path);
        await ToSignal(GetTree(), SceneTree.SignalName.TreeChanged);
        foreach (var node in GetTree().GetNodesInGroup("EntryPoints")) {
            if (node is EntryPoint entry) {
                if (entry.Name == entryPointName) {
                    MovePlayer(entry);
                    return;
                }
            }
        }
    }
    
    public void MovePlayer(EntryPoint entryPoint) {
        if (GetTree().GetFirstNodeInGroup("Player") is PlayerController player) {
            player.MoveToEntrypoint(entryPoint.GlobalPosition);
            Camera2D.ResetSmoothing();
            var tween = GetTree().CreateTween();
            tween.TweenProperty(ColorRect, "color:a", 0, 0.2);
            var newSceneName = GetTree().CurrentScene.SceneFilePath.GetBaseName();
            if (WorldState.ContainsKey(newSceneName))
            {
                FromDict(GetTree(), WorldState[newSceneName]);
            }
            
        }
    }

    public EnemyAliveState ToDict(SceneTree sceneTree)
    {
        var state = new EnemyAliveState();
        var list = new Array<string>();
        foreach (var node in sceneTree.GetNodesInGroup("Enemy"))
        {
            var path = GetPathTo(node);
            list.Add(path);
        }

        state.enemyAlive = list;
        return state;
    }
    
    public void FromDict(SceneTree sceneTree, EnemyAliveState aliveState)
    {
        
        foreach (var node in sceneTree.GetNodesInGroup("Enemy"))
        {
            var path = GetPathTo(node);
            var array = aliveState.enemyAlive;
            GD.Print(array.Count);
            if (!array.Contains(path))
            {
                node.QueueFree();
            }
        }
    }

    
    public void SaveGame()
    {
        var oldSceneName = GetTree().CurrentScene.SceneFilePath.GetBaseName();
        WorldState[oldSceneName] = ToDict(GetTree());
        PlayerStatus playerStatus = new PlayerStatus();
        playerStatus.Energy = Stat.CurrentEnergy;
        playerStatus.Health = Stat.CurrentHealth;
        playerStatus.SceneName = GetTree().CurrentScene.SceneFilePath;
        if (GetTree().GetFirstNodeInGroup("Player") is PlayerController player)
        {
            playerStatus.Position = player.GlobalPosition;
        }
        
        SavedData savedData = new SavedData();
        savedData.PlayerStatus = playerStatus;
        savedData.WorldState = WorldState;
        ResourceSaver.Save(savedData, UserDataFilePath);
    }
    
    
    public async void LoadGame()
    {
        if (!ResourceLoader.Exists(UserDataFilePath))
        {
            return;
        }

        SavedData savedData= ResourceLoader.Load<SavedData>(UserDataFilePath);
        var sceneName = savedData.PlayerStatus.SceneName;
        GetTree().ChangeSceneToFile(sceneName);
        WorldState = savedData.WorldState;
        await ToSignal(GetTree(), SceneTree.SignalName.TreeChanged);
        if (GetTree().GetFirstNodeInGroup("Player") is PlayerController player)
        {
            GD.Print("Load Player");
            player.Position = savedData.PlayerStatus.Position;
            Stat.CurrentHealth = savedData.PlayerStatus.Health;
            Stat.CurrentEnergy = savedData.PlayerStatus.Energy;
            Stat.SignalChanged();
        }
        
        if (WorldState.ContainsKey(sceneName))
        {
           
            FromDict(GetTree(), WorldState[sceneName]);
        }
    }

    public bool HasSaveFile()
    {
        return ResourceLoader.Exists(UserDataFilePath);
    }
}


