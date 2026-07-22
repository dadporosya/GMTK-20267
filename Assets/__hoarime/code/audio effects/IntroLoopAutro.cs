using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class IntroLoopAutro : MonoBehaviour
{
    // Статическая ссылка на единственный экземпляр (Синглтон)
    public static IntroLoopAutro Instance { get; private set; }

    [Header("Audio Clips")]
    public AudioClip introClip;
    public AudioClip loopClip;
    public AudioClip outroClip;

    [Header("Settings")]
    public bool playOnStart = true;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource activeSource;

    private double nextEventTime;
    private bool isPlaying = false;
    private bool stopRequested = false;

    void Awake()
    {
        // Если синглтон уже существует и это НЕ этот объект
        if (Instance != null && Instance != this)
        {
            // Просто уничтожаем дубликат, который пытается создаться в новой сцене
            Destroy(gameObject);
            return;
        }

        // Это самый первый инстанс — настраиваем его
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Важно: Инициализируем источники ТОЛЬКО для первого инстанса
        sourceA = GetComponent<AudioSource>();
        sourceB = gameObject.AddComponent<AudioSource>();

        CopySourceSettings(sourceA, sourceB);
    }


    void Start()
    {
        // Играем только если это самый первый запуск (инстанс)
        if (playOnStart && Instance == this && !isPlaying)
        {
            Play();
        }
    }

    public void Play()
    {
        if (isPlaying || introClip == null || loopClip == null) return;

        isPlaying = true;
        stopRequested = false;

        nextEventTime = AudioSettings.dspTime + 0.1;

        sourceA.clip = introClip;
        sourceA.loop = false;
        sourceA.PlayScheduled(nextEventTime);

        nextEventTime += (double)introClip.samples / introClip.frequency;

        sourceB.clip = loopClip;
        sourceB.loop = true;
        sourceB.PlayScheduled(nextEventTime);

        activeSource = sourceB;
    }

    void Update()
    {
        if (!isPlaying) return;

        if (stopRequested && activeSource == sourceB && sourceB.isPlaying)
        {
            double currentDspTime = AudioSettings.dspTime;
            double loopCycleLength = (double)loopClip.samples / loopClip.frequency;

            double elapsedInLoop = (currentDspTime - (nextEventTime - loopCycleLength)) % loopCycleLength;
            double timeToLoopEnd = loopCycleLength - elapsedInLoop;

            if (timeToLoopEnd < 0.5)
            {
                sourceB.loop = false;
                double outroStartTime = currentDspTime + timeToLoopEnd;

                if (outroClip != null)
                {
                    sourceA.clip = outroClip;
                    sourceA.loop = false;
                    sourceA.PlayScheduled(outroStartTime);
                }

                isPlaying = false;
                stopRequested = false;
            }
        }
    }

    public void StopAfterLoop()
    {
        if (!isPlaying) return;
        stopRequested = true;
    }

    public void StopImmediately()
    {
        isPlaying = false;
        stopRequested = false;
        sourceA.Stop();
        sourceB.Stop();
    }

    public void StopWithImmediateOutro()
    {
        if (!isPlaying) return;

        // Сбрасываем флаги работы основной логики Update
        isPlaying = false;
        stopRequested = false;

        // Мгновенно останавливаем играющий луп
        sourceB.Stop();

        // Если аутро назначено, запускаем его без задержек
        if (outroClip != null)
        {
            sourceA.clip = outroClip;
            sourceA.loop = false;
            sourceA.Play();
        }
    }


    private void CopySourceSettings(AudioSource from, AudioSource to)
    {
        to.outputAudioMixerGroup = from.outputAudioMixerGroup;
        to.volume = from.volume;
        to.pitch = from.pitch;
        to.spatialBlend = from.spatialBlend;
        to.minDistance = from.minDistance;
        to.maxDistance = from.maxDistance;
    }
}
