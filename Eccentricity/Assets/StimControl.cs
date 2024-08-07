using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Random = System.Random;
using System.IO;
using System;
using TMPro;
using Varjo.XR;

/* Requirements:
 * - This script must be attached to only one GameObject in the scene.
 * - All names in the first section of variables must correspond to real objects in the scene,
 *   and their names must match exactly, including case sensitivity.
 * - An object called "textInputCanvas" must exist in the scene.
 * - An object called "cue" must exist in the scene.
 * - Positions for displaying participant text and the cue should be assigned to variables "textPos" and "cuePos" respectively.
 */

public class StimControl : MonoBehaviour
{
    private bool cueOn = true;
    public bool isOvert = true;
    public double[] doublePos = {0.0, 15.0, 30.0, -15.0, -30.0}; // Different random horizontal positions
    public double[] doubleVertPos = {0.0, 10.0, 20.0, -10.0, -20.0}; // Different random vertical positions
    // Names of different stimuli (Unity object names)
    public string[] stimuli = { "CanvasE", "CanvasB", "CanvasP" };

    // Instruction text values
    public string[] instrTextValues = {
        // Instruction 1
        @"You will be reacting to three different
letters in this protocol, and pressing
the keys v, b, and n for each one.
Please try to react to the letters and
don't try to anticipate them.

Press Spacebar when ready.",
        // Instruction 2
        @"This is the letter E. Press v to continue.",
        // Instruction 3
        @"This is the letter B. Press b to continue.",
        // Instruction 4
        @"This is the letter P. Press n to continue.",
        // Instruction 5
        @"Here are some practice rounds to
familiarize you with the protocol.
Press Spacebar to begin.",
    };

    // Counter for finishing the program
    public int currentTrial = 1;
    public int trainingTrials = 3;
    public int trials = 5;

    // Key codes for different stimuli (Unity object names)
    private KeyCode[] keyCodes = { KeyCode.V, KeyCode.B, KeyCode.N };

    // Global variables for time
    private float preCue_time = 0.5f; // Wait time before cue is shown after trial ends
    private float cue_time = 0.2f; // Time that the cue is on screen
    private float time_min = 0.5f; // Minimum time between cue disappearing and stimulus    
    private float time_max = 1.5f; // Maximum time between cue disappearing and stimulus
    private float cueToStim_time = 0f; // Randomly set later in the code

    private int countdownTime = 5; // Time between training and experiment phase
    private int calibrationPeriodCycle = 0; // Time between training and experiment phase

    // Phase of the experiment
    public int phase = 1;
    private bool in_use = false;    // Avoid user clicking multiple buttons at the same time
    private bool start = false;     // Indicates the first trial
    /*
     * Phase -1, -2, -3... = In-between phase 1, 2, or 3, while co-routines are running
     * Phase 0 = *Unused for now*
     * Phase 1 = Waiting for eye tracking to calibrate
     * Phase 2 = Name input
     * Phase 3 = Start / Instructions
     * Phase 4 = Training phase
     * Phase 5 = Break 
     * Phase 6 = Data taking phase
     * Phase 7 = Thank you screen / Demographics survey reminder
     * in_use = Currently going through the change coroutine, has not shown the next stimulus yet
     */

    // Misc variables
    static string dataPath = Directory.GetCurrentDirectory() + "/Assets/Data/";
    public string logFile; // File name, set in phase 0 after getting participant name
    Random rnd = new Random();
    private string responseKey = "";
    private string log; // New line of data
    private int instrNum = 0; // Index used to increment instructions
    private int posIndex, posVertIndex, stimIndex; // Indices for position and stimuli respectively randomized later in code (need global scope since they're used in multiple functions)
    private GameObject instrText; // Text object for instructions
    private TMP_InputField nameInputField; // UI object for name input
    public string participantID;
    public EventSystem eventSystem;

    double getPosfromDeg(double x)
    {
        // Convert degrees to radians since Math.Tan expects radians
        double radians = x * Math.PI / 180.0;
        // Calculate tan(x) and then divide by 2
        double result = Math.Tan(radians) / 2.0;
        Debug.Log(x);
        Debug.Log(result);
        return result;
    }

    IEnumerator change()
    {
        // Randomizes stimulus every round
        // Shows stimulus
        posIndex = rnd.Next(0, doublePos.Length);
        posVertIndex = rnd.Next(0, doubleVertPos.Length);
        stimIndex = rnd.Next(0, stimuli.Length);
        currentTrial++;

        yield return new WaitForSecondsRealtime(preCue_time); // Wait before trial starts

        if (cueOn == true)
        {
            GameObject.Find("cue").transform.position = GameObject.Find("cuePos").transform.position; // Cue appears at center
            log = DateTimeOffset.Now.ToUnixTimeMilliseconds() + ","; // CueShowTime
            yield return new WaitForSecondsRealtime(cue_time); // Cue stays there for this long
            if (isOvert)
            {
                GameObject.Find("cue").transform.position = GameObject.Find("disappearPos").transform.position; // Cue disappears
            }
        }
        else
        {
            log = DateTimeOffset.Now.ToUnixTimeMilliseconds() + ","; // CueShowTime
        }

        cueToStim_time = (float)((rnd.NextDouble() * (time_max - time_min)) + time_min);

        // Waits before showing stimulus
        yield return new WaitForSecondsRealtime(cueToStim_time);

        // Shows stimulus
        GameObject.Find(stimuli[stimIndex]).transform.position = new Vector3((float)getPosfromDeg(doublePos[posIndex]), (float)getPosfromDeg(doubleVertPos[posVertIndex]), 0f);
        if (!isOvert && doublePos[posIndex] == 0)
        {
            GameObject.Find("cue").transform.position = GameObject.Find("disappearPos").transform.position; // Cue disappears
        }
        log += DateTimeOffset.Now.ToUnixTimeMilliseconds() + ","; // ObjShowTime
        start = true;
        in_use = false;
    }

    IEnumerator phase1() // Eye Tracking Calibration
    {
        phase *= -1;
        // Update the text with the current number of periods
        instrText.GetComponent<TextMeshPro>().text = 
        @"(Press Backslash to restart calibration)
        Eye tracking calibration" + new string('.', calibrationPeriodCycle);
        // Increment the period count, reset if it reaches 4
        calibrationPeriodCycle = (calibrationPeriodCycle + 1) % 4;

        yield return new WaitForSecondsRealtime(1);
        // If calibration ends, sets up the next phase
        if (VarjoEyeTracking.IsGazeCalibrated())
        {
            // Removes text
            instrText.GetComponent<TextMeshPro>().text = "";
            instrText.transform.position = GameObject.Find("disappearPos").transform.position;
            GameObject.Find("eyeTracking").GetComponent<EyeTrackingControl>().StartLogging();
            // Sets up for phase 2
            GameObject.Find("textInputCanvas").transform.position = GameObject.Find("textPos").transform.position;
            phase = 2;
            yield break;
        }
        phase *= -1;
    }

    void phase2() // Participant name/ID input phase
    {
        // Selects Input Field if space is pressed. Workaround for Unity Bug
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // Set the input field as the currently selected object
            EventSystem.current.SetSelectedGameObject(nameInputField.gameObject, null);

            // Optionally, focus on the input field to start typing immediately
            nameInputField.ActivateInputField();
        }
        // Creates data file and sets participant name
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            participantID = nameInputField.text;
            string ovcoString = "covert";
            if (isOvert)
            {
                ovcoString = "overt";
            }

            // Creates data folder / file
            logFile = dataPath + participantID + "-" + ovcoString + "rtData-" + System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv";
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }
            File.WriteAllText(logFile, "CueShowTime,ObjShowTime,ReactionTime,Eccentricity,VerticalEccentricity,StimType,Guess,Correct\n");
            Debug.Log($"Data file started for {nameInputField.text}");

            // Moves canvas to behind the plane
            GameObject.Find("textInputCanvas").transform.position = GameObject.Find("disappearPos").transform.position; // Canvas disappears

            // Warns if logging has not started
            bool loggingStarted = GameObject.Find("eyeTracking").GetComponent<EyeTrackingControl>().logging;
            if (!loggingStarted)
            {
                Debug.Log("Eye tracking was not started.");
            }

            // Sets things up for phase 3, showing instruction 1
            phase = 3;
            instrText.GetComponent<TextMeshPro>().text = instrTextValues[instrNum];
            instrText.transform.position = GameObject.Find("textPos").transform.position;
            return;
        }
    }

    void phase3() // Start and instruction phase
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (instrNum >= 0 && instrNum < stimuli.Length)
            {
                // Move to the next instruction phase
                instrNum++;
                instrText.GetComponent<TextMeshPro>().text = instrTextValues[instrNum];
                GameObject.Find(stimuli[instrNum - 1]).transform.position = GameObject.Find("deg0").transform.position;
            }
            else if (instrNum == stimuli.Length)
            {
                // Describes training rounds and removes the last stimulus
                instrNum++;
                instrText.GetComponent<TextMeshPro>().text = instrTextValues[instrNum];
                GameObject.Find(stimuli[instrNum - 2]).transform.position = GameObject.Find("disappearPos").transform.position;
            }
            else if (instrNum == stimuli.Length + 1)
            {
                // Removes instruction text and sets up phase 4
                instrText.transform.position = GameObject.Find("disappearPos").transform.position;
                // Setup for phase 4, starts the first training trial
                phase = 4;
                StartCoroutine(change());
            }
        }

        // Check for moving to the next stimuli/instruction phases
        if (instrNum > 0 && instrNum <= stimuli.Length)
        {
            if (Input.GetKeyDown(KeyCode.V) && instrNum == 1)
            {
                instrNum++;
                instrText.GetComponent<TextMeshPro>().text = instrTextValues[instrNum];
                GameObject.Find(stimuli[instrNum - 2]).transform.position = GameObject.Find("disappearPos").transform.position;
                if (stimuli.Length >= instrNum)
                {
                    GameObject.Find(stimuli[instrNum - 1]).transform.position = GameObject.Find("deg0").transform.position;
                }
            }
            else if (Input.GetKeyDown(KeyCode.B) && instrNum == 2)
            {
                instrNum++;
                instrText.GetComponent<TextMeshPro>().text = instrTextValues[instrNum];
                GameObject.Find(stimuli[instrNum - 2]).transform.position = GameObject.Find("disappearPos").transform.position;
                if (stimuli.Length >= instrNum)
                {
                    GameObject.Find(stimuli[instrNum - 1]).transform.position = GameObject.Find("deg0").transform.position;
                }
            }
            else if (Input.GetKeyDown(KeyCode.N) && instrNum == 3)
            {
                instrNum++;
                instrText.GetComponent<TextMeshPro>().text = instrTextValues[instrNum];
                GameObject.Find(stimuli[instrNum - 2]).transform.position = GameObject.Find("disappearPos").transform.position;
            }
        }
    }

    IEnumerator phase4() // Training phase
    {
        phase *= -1;
        // Checks if a trial is currently running
        if (!in_use)
        {
            // Sets response key
            if (Input.GetKeyDown(KeyCode.V)) { responseKey = stimuli[0]; }
            else if (Input.GetKeyDown(KeyCode.B)) { responseKey = stimuli[1]; }
            else if (Input.GetKeyDown(KeyCode.N)) { responseKey = stimuli[2]; }
            // If one of the buttons has been pressed, log data and set up next trial
            if (responseKey != "")
            {
                in_use = true;
                // Displays correct or incorrect for the participant
                if (stimuli[stimIndex] == responseKey)
                {
                    instrText.GetComponent<TextMeshPro>().text = "Correct!";
                    instrText.transform.position = GameObject.Find("textPos").transform.position;
                    yield return new WaitForSecondsRealtime(1.5f);
                    instrText.transform.position = GameObject.Find("disappearPos").transform.position;
                }
                else
                {
                    instrText.GetComponent<TextMeshPro>().text = "Incorrect.";
                    instrText.transform.position = GameObject.Find("textPos").transform.position;
                    yield return new WaitForSecondsRealtime(1.5f);
                    instrText.transform.position = GameObject.Find("disappearPos").transform.position;
                }
                // Removes all stimuli to behind the plane
                for (int k = 0; k < stimuli.Length; k++)
                {
                    GameObject.Find(stimuli[k]).transform.position = GameObject.Find("disappearPos").transform.position;
                }
                if (!isOvert)
                {
                    GameObject.Find("cue").transform.position = GameObject.Find("disappearPos").transform.position; // Cue disappears
                }
                // Resets response key
                responseKey = "";

                // If the number of training trials specified has been reached, set up phase 5
                if (currentTrial > trainingTrials)
                {
                    // Removes correct or incorrect
                    instrText.GetComponent<TextMeshPro>().text = "";
                    instrText.transform.position = GameObject.Find("disappearPos").transform.position;
                    // Resets trial counter, and sets up phase 5
                    currentTrial = 1;
                    phase = 5;
                    yield break;
                }
                // Starts the next trial
                StartCoroutine(change());
            }
        }
        phase *= -1;
    }

    IEnumerator phase5() // Break phase
    {
        phase *= -1;
        // Shows and updates text for the break
        instrText.GetComponent<TextMeshPro>().text = $@"Training has finished.
The experiment will begin in {countdownTime} seconds";
        instrText.transform.position = GameObject.Find("textPos").transform.position;
        yield return new WaitForSecondsRealtime(1f);
        countdownTime -= 1;
        // If countdown reaches 0, sets up the next phase
        if (countdownTime == 0)
        {
            // Removes text
            instrText.GetComponent<TextMeshPro>().text = "";
            instrText.transform.position = GameObject.Find("disappearPos").transform.position;
            // Sets up phase 6, starting the first trial
            StartCoroutine(change());
            start = false;
            phase = 6;
            yield break;
        }
        phase *= -1;
    }

    void phase6() // Data taking phase
    {
        // Checks if a trial is currently running
        if (!in_use)
        {
            // Sets response key
            if (Input.GetKeyDown(KeyCode.V)) { responseKey = stimuli[0]; }
            else if (Input.GetKeyDown(KeyCode.B)) { responseKey = stimuli[1]; }
            else if (Input.GetKeyDown(KeyCode.N)) { responseKey = stimuli[2]; }
            // If one of the buttons has been pressed, log data and set up next trial
            if (responseKey != "")
            {
                in_use = true;
                // Only logs data after the first trial that started in the last phase
                if (start)
                {
                    // Logs data
                    log += DateTimeOffset.Now.ToUnixTimeMilliseconds() + ","; // ReactionTime

                    // Shows stimulus
                    log += doublePos[posIndex] + "," + doubleVertPos[posVertIndex] + "," + stimuli[stimIndex] + "," + responseKey + ","; // independentVar, StimType, Guess
                    if (stimuli[stimIndex] == responseKey)
                    {
                        log += "True\n";
                    }
                    else
                    {
                        log += "False\n";
                    }
                    File.AppendAllText(logFile, log);
                    log = "";
                }
                // Removes stimuli to behind the plane
                for (int k = 0; k < stimuli.Length; k++)
                {
                    GameObject.Find(stimuli[k]).transform.position = GameObject.Find("disappearPos").transform.position;
                }
                if (!isOvert)
                {
                    GameObject.Find("cue").transform.position = GameObject.Find("disappearPos").transform.position; // Cue disappears   
                }
                // Resets the response key
                responseKey = "";
                // If the number of experimental trials specified has been reached, go to the next phase
                if (currentTrial > trials)
                {
                    phase = 7;
                    return;
                }
                StartCoroutine(change());
            }
        }
    }

    IEnumerator phase7() // Thank you screen / demographics survey reminder
    {
        phase *= -1;
        // Shows text for 2 seconds and ends the protocol
        instrText.GetComponent<TextMeshPro>().text = @"Thank you for taking data for us!
Please take your demographics survey now";
        instrText.transform.position = GameObject.Find("textPos").transform.position;
        yield return new WaitForSecondsRealtime(2f);
        UnityEditor.EditorApplication.isPlaying = false;
        phase *= -1;
    }

    void Start()
    {
        // Initializes variables
        instrText = GameObject.Find("instrText"); // Text object used
        nameInputField = GameObject.Find("nameInputField").GetComponent<TMP_InputField>(); // UI object for name input
        eventSystem = GameObject.Find("EventSystem").GetComponent<EventSystem>();
        instrText.transform.position = GameObject.Find("textPos").transform.position;
    }

    void Update()
    {
        // Checks if escape is pressed to end the protocol
        if (Input.GetKey(KeyCode.Escape))
        {
            // This only works in editor view
            UnityEditor.EditorApplication.isPlaying = false;
            // This only works for built programs
            // Application.Quit();
        }
        // Runs code depending on which phase is currently ongoing
        else if (phase < 0)
        {
            return;
        }
        else if (phase == 1) // In instructions / start phase
        {
            StartCoroutine(phase1());
        }
        else if (phase == 2) // In training phase
        {
            phase2();
        }
        else if (phase == 3) // Break between training and data taking
        {
            phase3();
        }
        else if (phase == 4) // In data taking phase
        {
            StartCoroutine(phase4());
        }
        else if (phase == 5) // Thank you / demographics survey reminder
        {
            StartCoroutine(phase5());
        }
        else if (phase == 6) // In data taking phase
        {
            phase6();
        }
        else if (phase == 7) // In data taking phase
        {
            StartCoroutine(phase7());
        }
    }

    void OnApplicationQuit()
    {
        // Adds PC info to the data file
        // Check if the file exists
        string pcDataFilePath = Directory.GetCurrentDirectory() + "/Assets/Data/runData.csv";
        if (!File.Exists(pcDataFilePath))
        {
            // Create file and write headers
            using (StreamWriter writer = new StreamWriter(pcDataFilePath))
            {
                writer.WriteLine("cpuID,file,Trials");
            }
        }
        // Append the computer name and time to the file
        using (StreamWriter writer = File.AppendText(pcDataFilePath))
        {
            string computerName = SystemInfo.deviceName;
            string pcID = SystemInfo.deviceUniqueIdentifier;
            currentTrial--;

            string nameAndTime = logFile;
            int lastIndex = Math.Max(logFile.LastIndexOf('/'), logFile.LastIndexOf('\\'));
            // If a slash or backslash is found, return the substring from just after it
            if (lastIndex != -1)
            {
                nameAndTime = logFile.Substring(lastIndex + 1);
            }

            writer.WriteLine($"{computerName},{pcID},{nameAndTime},{currentTrial}");
        }
    }
}
