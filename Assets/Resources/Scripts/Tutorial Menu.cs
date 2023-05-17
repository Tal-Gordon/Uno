using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class TutorialMenu : MonoBehaviour
{
    public GameObject tutorialWindow;
    public GameObject gameRulesWindow;
    public GameObject houseRulesWindow;
    public GameObject bugsWindow;

    public ToggleGroup toggleGroup;
    public Toggle gameRulesToggle;
    public Toggle houseRulesToggle;
    public Toggle bugsToggle;
    void Start()
    {
        
    }
    void Update()
    {
        
    }
    public Toggle CurrentSelection
    {
        get { return toggleGroup.ActiveToggles().FirstOrDefault(); }
    }
    public void SelectToggle(int id)
    {
        var toggles = toggleGroup.GetComponentsInChildren<Toggle>();
        toggles[id].SetIsOnWithoutNotify(true);
        toggles[id].image.color = Color.white;
        //Debug.Log("Toggle " + currentSelection.name + " is selected.");
        foreach (Toggle toggle in toggles)
        {
            if (!toggle.isOn)
                toggle.image.color = new Color32(175, 175, 175, 255);
        }
        GameRulesTab();
        HouseRulesTab();
        BugsTab();
    }
    public void TutorialTab()
    {
        if (tutorialWindow.activeSelf) { tutorialWindow.SetActive(false); }
        else { tutorialWindow.SetActive(true); }
    }
    private void GameRulesTab()
    {
        if (CurrentSelection.name == "Game Rules")
            gameRulesWindow.SetActive(true);
        else
            gameRulesWindow.SetActive(false);
    }
    private void HouseRulesTab()
    {
        if (CurrentSelection.name == "House Rules")
            houseRulesWindow.SetActive(true);
        else
            houseRulesWindow.SetActive(false);
    }
    private void BugsTab()
    {
        if (CurrentSelection.name == "Bugs")
            bugsWindow.SetActive(true);
        else
            bugsWindow.SetActive(false);
    }
}
