using LevelGeneration;
using UnityEngine;
using UnityEngine.UI;

public class MainUI : MonoBehaviour
{
    public LevelGenerator levelGenerator;
    public CameraController cameraController;

    private bool _isGenerating;

    public Text widthText;
    public Text heightText;
    public Text rateText;

    public int totalAttempts = 0;
    private int successes = 0;
    private int failures = 0;

    public void RunSuceeded(){
        totalAttempts++;
        successes++;
        SetRateText();
    }

    public void RunFailed(){
        totalAttempts++;
        failures++;
        SetRateText();
    }

    private void SetRateText(){
        float successRate = (totalAttempts > 0) ? (float)successes / totalAttempts * 100 : 0;
        float failRate = (totalAttempts > 0) ? (float)failures / totalAttempts * 100 : 0;

        rateText.text = "Total Attempts: " + totalAttempts + "\nSuccess Rate: " + successRate.ToString("F2") + "%\nFail Rate: " + failRate.ToString("F2") + "%";
    }

    public void Generate()
    {
        if (_isGenerating) return;
        _isGenerating = true;

        levelGenerator.GenerateLevel();

        cameraController.AdjustCamera(levelGenerator.width, levelGenerator.height);

        _isGenerating = false;
    }

    public void ChangeWidth(float width)
    {
        levelGenerator.width = Mathf.RoundToInt(width);
        widthText.text = width.ToString();
    }

    public void ChangeHeight(float height)
    {
        levelGenerator.height = Mathf.RoundToInt(height);
        heightText.text = height.ToString();
    }
}