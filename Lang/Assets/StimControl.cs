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
    // current sentence height in degrees
    public float sentenceHeight = 1f;

    // Instruction text values
    public string[] instrTextValues = {
    @"Hello! You are going be presented with
    fifty sentences. Read each sentence
    throughoutly and press [spacebar] once
    finished. Now, if you are ready,
    press [spacebar] to continue. "
    };

    public string[] stimTextValues = {
    @"I bought a new blue shirt.",
    @"It fits really well on me.",
    @"Blue is my favorite color now.",
    @"I wear it almost every day.",
    @"My friends say it looks great.",

    @"She moved to a new city.",
    @"Her apartment is really cozy.",
    @"She found a job nearby.",
    @"She walks to work daily.",
    @"She misses her old friends.",

    @"He started learning the guitar.",
    @"He practices for two hours daily.",
    @"His fingers hurt from the strings.",
    @"He can play three songs now.",
    @"He wants to start a band.",

    @"We went to the beach yesterday.",
    @"The sand was warm and soft.",
    @"We built a big sand castle.",
    @"The waves were very calming.",
    @"We stayed till the sunset.",

    @"The cat sleeps on the sofa.",
    @"It chases its tail often.",
    @"At night, it hunts for mice.",
    @"It purrs when it's happy.",
    @"The cat dislikes taking baths.",

    @"They planted a garden last spring.",
    @"Tomatoes grow next to the fence.",
    @"They water the plants every morning.",
    @"Sunflowers reach towards the sky.",
    @"Gardening brings them lots of joy.",

    @"She makes a great chocolate cake.",
    @"Her cakes are famous in town.",
    @"She bakes every Saturday morning.",
    @"People come to buy her cakes.",
    @"Baking is her favorite hobby.",

    @"He reads a new book weekly.",
    @"He loves mystery and adventure genres.",
    @"Books fill his room's shelves.",
    @"He reviews them online for others.",
    @"Reading is his way to relax.",

    @"They go cycling on weekends.",
    @"They ride through the park's paths.",
    @"Their bikes are bright and shiny.",
    @"Cycling keeps them fit and happy.",
    @"They race each other often.",

    @"I started writing a daily journal.",
    @"I write about my day's events.",
    @"It helps me clear my mind.",
    @"I use a pen and notebook.",
    @"Writing has become my routine." };

    // Counter for finishing the program
    public int currentTrial = 1;
    public int trainingTrials = 3;
    public int trials = 50;
    public int stimNum = 0;

    // Global variables for time
    private float preCue_time = 0.5f; // Wait time before cue is shown after trial ends
    private float cue_time = 0.2f; // Time that the cue is on screen
    private float cueToStim_time = 0f; // 0

    private int countdownTime = 5; // Time between training and experiment phase
    private int calibrationPeriodCycle = 0; // Time between training and experiment phase

    // Phase of the experiment
    public int phase = 1;
    private bool in_use = false;    // Avoid user clicking multiple buttons at the same time
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
    private string log; // New line of data
    private GameObject instrText; // Text object for instructions
    private GameObject stimText; // text object for showing stimuli
    private TMP_InputField nameInputField; // UI object for name input
    public string participantID;
    public EventSystem eventSystem;
    private int instrNum = 0;

    IEnumerator change()
    {
        currentTrial++;

        yield return new WaitForSecondsRealtime(preCue_time); // Wait before trial starts

        GameObject.Find("cue").transform.position = GameObject.Find("cuePos").transform.position; // Cue appears at center
        log = DateTimeOffset.Now.ToUnixTimeMilliseconds() + ","; // CueShowTime
        yield return new WaitForSecondsRealtime(cue_time); // Cue stays there for this long
        GameObject.Find("cue").transform.position = GameObject.Find("disappearPos").transform.position; // Cue disappears

        // Waits before showing stimulus
        yield return new WaitForSecondsRealtime(cueToStim_time);

        // Shows stimulus
        stimText.GetComponent<TextMeshPro>().text = stimTextValues[stimNum];
        stimNum++;
        stimText.transform.position = GameObject.Find("deg0").transform.position; // StimType appears
        log += DateTimeOffset.Now.ToUnixTimeMilliseconds() + ","; // ObjShowTime
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

            // Creates data folder / file
            logFile = dataPath + participantID + "-" + "rtData-" + System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv";
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }
            File.WriteAllText(logFile, "CueShowTime,ObjShowTime,ReactionTime,SentenceNum\n");
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
            // Removes instruction text and sets up phase 4
                instrText.GetComponent<TextMeshPro>().text = "";
                instrText.transform.position = GameObject.Find("disappearPos").transform.position;
            // Setup for phase 4, starts the first training trial
            phase = 4;
        }
    }

    IEnumerator phase4() // Break phase
    {
        phase *= -1;
        // Shows and updates text for the break
        instrText.GetComponent<TextMeshPro>().text = $"The experiment will begin in {countdownTime} seconds";
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
            phase = 5;
            yield break;
        }
        phase *= -1;
    }

    void phase5() // Data taking phase
    {
        // Checks if a trial is currently running
        if (!in_use)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                in_use = true;
                // Logs data
                log += DateTimeOffset.Now.ToUnixTimeMilliseconds() + ","; // ReactionTime
                log += stimNum + "\n"; // sentenceNum
                File.AppendAllText(logFile, log);
                log = "";
                stimText.transform.position = GameObject.Find("disappearPos").transform.position; // StimText disappears
                // If the number of experimental trials specified has been reached, go to the next phase
                if (currentTrial > trials)
                {
                    phase = 6;
                    return;
                }
                StartCoroutine(change());
            }
        }
    }

    IEnumerator phase6() // Thank you screen / demographics survey reminder
    {
        phase *= -1;
        // Shows text for 2 seconds and ends the protocol
        instrText.GetComponent<TextMeshPro>().text = "Thank you for taking data for us! Please take your demographics survey now";
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
        stimText = GameObject.Find("stimText");
        Vector3 newScale = new Vector3(sentenceHeight, sentenceHeight, sentenceHeight); // Create a new Vector3 with the scale value for x, y, and z
        GameObject.Find("stimText").transform.localScale = newScale;
        GameObject.Find("cue").transform.localScale = newScale;
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
        else if (phase == 4) // Thank you / demographics survey reminder
        {
            StartCoroutine(phase4());
        }
        else if (phase == 5) // In data taking phase
        {
            phase5();
        }
        else if (phase == 6) // In data taking phase
        {
            StartCoroutine(phase6());
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
