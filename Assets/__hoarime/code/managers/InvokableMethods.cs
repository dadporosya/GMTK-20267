using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.VFX;

public class InvokableMethods : MonoBehaviour
{
    [SerializeField] float sceneTransitionWaitTime;
    [SerializeField] float sceneTransitionTime;
    [SerializeField] float sceneTransitionDelay;
    [SerializeField] float fadeInSoundTime;
    [SerializeField] float fadeLPFTime;
    [SerializeField] float fadePitchSoundTime;
    [SerializeField] bool disableSoundOnSceneTransition;

    [SerializeField] float vfxPrefarmTime;

    [SerializeField] char textSeparator;
    [SerializeField] Image blackoutBG;

    [SerializeField] Sprite[] blinkImages;
    [SerializeField] Image blinkImageParent;
    [SerializeField] float blinkLastFrameFadeTime;

    [SerializeField] float weirdPitch;

    [SerializeField] Image textBlackoutBG;
    [SerializeField] float blinkTransitionTextTime;
    [SerializeField] float epilepsyFlashDelay;

    [SerializeField] bool freezeBlinkAtEnd;

    [SerializeField] UnityEvent onEndBlinkText;
    [SerializeField] UnityEvent onEpilepsyEnd;

    [SerializeField] Volume globalVolume;
    [SerializeField] Color carouselFogColor;
    [SerializeField] Color defaultFogColor;

    [SerializeField] float bloomFadeTime;


    private Camera mainCam;
    private Monologue monologue;

    private void Awake()
    {
        AudioListener.volume = 1;
        monologue = FindFirstObjectByType<Monologue>();
        mainCam = Camera.main;
    }
    public void SendMonologueText(string text)
    {
        string[] separatedText = SplitString(text, textSeparator);
        monologue.MonologueText(separatedText);
    }

    public void LoadSceneByName(string name)
    {
        SceneManager.LoadScene(name);
    }

    public void ContinueMusicAfterLoop()
    {
        IntroLoopAutro.Instance.StopAfterLoop();
    }

    public void StopLoopImmediatly()
    {
        IntroLoopAutro.Instance.StopWithImmediateOutro();
    }


    public void RoughSceneTransition(string name)
    {
        StartCoroutine(RoughSceneTransition_c(name));

    }

    private IEnumerator RoughSceneTransition_c(string name)
    {
        yield return new WaitForSeconds(sceneTransitionDelay);
        if (disableSoundOnSceneTransition)
        {
            AudioListener.volume = 0;
        }
        blackoutBG.color = new Color(blackoutBG.color.r, blackoutBG.color.g, blackoutBG.color.b, 1);
        blackoutBG.gameObject.SetActive(true);
        yield return new WaitForSeconds(sceneTransitionWaitTime);
        SceneManager.LoadScene(name);
    }

    public void SetBGObject(Image bg)
    {
        blackoutBG = bg;
    }


    public void SmoothSceneTransition(string name)
    {
        StartCoroutine(SmoothSceneTransition_c(name));
    }

    public void FadeInSound(AudioSource soundSource)
    {
        StartCoroutine(FadeInSoundCoroutine(soundSource));
    }

    private IEnumerator FadeInSoundCoroutine(AudioSource soundSource)
    {
        float targetVolume = soundSource.volume;
        float startVolume = 0;

        soundSource.volume = startVolume;
        soundSource.Play();

        float elapsedTime = 0;
        float progress;

        while (elapsedTime < fadeInSoundTime)
        {
            progress = Mathf.Clamp01(elapsedTime / fadeInSoundTime);
            soundSource.volume = Mathf.Lerp(startVolume, targetVolume, progress * progress);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        soundSource.volume = targetVolume;
    }

    public void FadeOutSound(AudioSource soundSource)
    {
        StartCoroutine(FadeOutSoundCoroutine(soundSource));
    }

    private IEnumerator FadeOutSoundCoroutine(AudioSource soundSource)
    {
        float targetVolume = 0;
        float startVolume = soundSource.volume;

        soundSource.volume = startVolume;

        float elapsedTime = 0;
        float progress;

        while (elapsedTime < fadeInSoundTime)
        {
            progress = Mathf.Clamp01(elapsedTime / fadeInSoundTime);
            soundSource.volume = Mathf.Lerp(startVolume, targetVolume, progress * progress);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        soundSource.volume = targetVolume;
    }

    public void FadeOutLFP(AudioLowPassFilter soundSource)
    {
        StartCoroutine(FadeOutLPFCoroutine(soundSource));
    }

    public void FadeInLFP(AudioLowPassFilter soundSource)
    {
        StartCoroutine(FadeInLPFCoroutine(soundSource));
    }

    private IEnumerator FadeOutLPFCoroutine(AudioLowPassFilter soundSource)
    {
        float targetVolume = 0;
        float startVolume = soundSource.cutoffFrequency;

        soundSource.cutoffFrequency = startVolume;

        float elapsedTime = 0;
        float progress;

        while (elapsedTime < fadeLPFTime)
        {
            progress = Mathf.Clamp01(elapsedTime / fadeLPFTime);
            soundSource.cutoffFrequency = Mathf.Lerp(startVolume, targetVolume, progress);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        soundSource.cutoffFrequency = targetVolume;
    }

    private IEnumerator FadeInLPFCoroutine(AudioLowPassFilter soundSource)
    {
        float targetVolume = soundSource.cutoffFrequency;
        float startVolume = 0;

        soundSource.cutoffFrequency = startVolume;

        float elapsedTime = 0;
        float progress;

        while (elapsedTime < fadeLPFTime)
        {
            progress = Mathf.Clamp01(elapsedTime / fadeLPFTime);
            soundSource.cutoffFrequency = Mathf.Lerp(startVolume, targetVolume, progress);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        soundSource.cutoffFrequency = targetVolume;
    }

    public void FadeToWeirdPitch(AudioSource soundSource)
    {
        StartCoroutine(FadeToWeirdPitch_c(soundSource));
    }

    public void FadeToNormalPitch(AudioSource soundSource)
    {
        StartCoroutine(FadeToNormalPitch_c(soundSource));
    }

    private IEnumerator FadeToWeirdPitch_c(AudioSource soundSource)
    {
        float targetPitch = weirdPitch;
        float startPitch = soundSource.pitch;

        float elapsedTime = 0;
        float progress;

        while (elapsedTime < fadePitchSoundTime)
        {
            progress = Mathf.Clamp01(elapsedTime / fadePitchSoundTime);
            soundSource.pitch = Mathf.Lerp(startPitch, targetPitch, progress * progress);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        soundSource.volume = targetPitch;
    }

    private IEnumerator FadeToNormalPitch_c(AudioSource soundSource)
    {
        float targetPitch = 1;
        float startPitch = soundSource.pitch;

        float elapsedTime = 0;
        float progress;

        while (elapsedTime < fadePitchSoundTime)
        {
            progress = Mathf.Clamp01(elapsedTime / fadePitchSoundTime);
            soundSource.pitch = Mathf.Lerp(startPitch, targetPitch, progress * progress);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        soundSource.volume = targetPitch;
    }

    private IEnumerator SmoothSceneTransition_c(string name)
    {
        yield return new WaitForSeconds(sceneTransitionDelay);
        blackoutBG.gameObject.SetActive(true);
        float elapsedTime = 0;
        float progress;
        blackoutBG.color = new Color(blackoutBG.color.r, blackoutBG.color.g, blackoutBG.color.b, 0);
        Color startingColor = blackoutBG.color;
        Color targetColor = startingColor;
        Color currentColor = startingColor;
        targetColor.a = 1;

        while (elapsedTime < sceneTransitionTime)
        {
            progress = Mathf.Clamp01(elapsedTime / sceneTransitionTime);
            currentColor.a = Mathf.Lerp(startingColor.a, targetColor.a, progress);
            blackoutBG.color = currentColor;
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        if (disableSoundOnSceneTransition)
        {
            AudioListener.volume = 0;
        }
        yield return new WaitForSeconds(sceneTransitionWaitTime);

        SceneManager.LoadScene(name);
    }

    public void BlinkImages(float blinkDelay)
    {
        StartCoroutine(BlinkImagesCoroutine(blinkDelay));

    }

    private IEnumerator BlinkImagesCoroutine(float blinkDelay)
    {
        blinkImageParent.gameObject.SetActive(true);
        WaitForSeconds blinkDelayCached = new WaitForSeconds(blinkDelay);

        foreach (Sprite image in blinkImages)
        {
            blinkImageParent.sprite = image;
            yield return blinkDelayCached;
        }

        float elapsedTime = 0;
        float progress;
        blinkImageParent.color = new Color(blinkImageParent.color.r, blinkImageParent.color.g, blinkImageParent.color.b, 1);
        Color startingColor = blinkImageParent.color;
        Color targetColor = startingColor;
        Color currentColor = startingColor;
        targetColor.a = 0;

        while (elapsedTime < blinkLastFrameFadeTime)
        {
            progress = Mathf.Clamp01(elapsedTime / blinkLastFrameFadeTime);
            currentColor.a = Mathf.Lerp(startingColor.a, targetColor.a, progress);
            blinkImageParent.color = currentColor;
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        blinkImageParent.gameObject.SetActive(false);

    }

    public void FadeInScene()
    {
        StartCoroutine(FadeInScene_c());
    }

    public void PrewarmVFX(VisualEffect vfx)
    {
        vfx.Stop();
        vfx.Reinit();
        vfx.Play();

        int steps = Mathf.RoundToInt(vfxPrefarmTime * 60);

        for (int i = 0; i < steps; i++)
        {
            vfx.Simulate(1 / 60f);
        }

    }

    public void MuteAllVolume(bool mute)
    {
        if (mute)
        {
            AudioListener.volume = 0;
        }
        else
        {
            AudioListener.volume = 1;
        }

    }

    public void FadeInAllVolume(float duration)
    {
        StartCoroutine(FadeInAllVolumeCoroutine(duration));
    }

    private IEnumerator FadeInAllVolumeCoroutine(float duration)
    {
        float targetVolume = AudioListener.volume;
        float startVolume = 0;

        AudioListener.volume = startVolume;

        float elapsedTime = 0;
        float progress;

        while (elapsedTime < duration)
        {
            progress = Mathf.Clamp01(elapsedTime / duration);
            AudioListener.volume = Mathf.Lerp(startVolume, targetVolume, progress * progress);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        AudioListener.volume = targetVolume;
    }

    public void FadeOutAllVolume(float duration)
    {
        StartCoroutine(FadeOutAllVolumeCoroutine(duration));
    }

    private IEnumerator FadeOutAllVolumeCoroutine(float duration)
    {
        float targetVolume = 0;
        float startVolume = AudioListener.volume;

        AudioListener.volume = startVolume;

        float elapsedTime = 0;
        float progress;

        while (elapsedTime < duration)
        {
            progress = Mathf.Clamp01(elapsedTime / duration);
            AudioListener.volume = Mathf.Lerp(startVolume, targetVolume, progress * progress);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        AudioListener.volume = targetVolume;
    }



    private IEnumerator FadeInScene_c()
    {
        blackoutBG.gameObject.SetActive(true);
        float elapsedTime = 0;
        float progress;
        blackoutBG.color = new Color(blackoutBG.color.r, blackoutBG.color.g, blackoutBG.color.b, 1);
        Color startingColor = blackoutBG.color;
        Color targetColor = startingColor;
        Color currentColor = startingColor;
        targetColor.a = 0;

        while (elapsedTime < sceneTransitionTime)
        {
            progress = Mathf.Clamp01(elapsedTime / sceneTransitionTime);
            currentColor.a = Mathf.Lerp(startingColor.a, targetColor.a, progress);
            blackoutBG.color = currentColor;
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        blackoutBG.gameObject.SetActive(false);

    }



    public static string[] SplitString(string str, char separator)
    {
        return str.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
    }

    public void TestMethod()
    {
        Debug.Log("test");
    }

    public void InvertBlackoutColor()
    {
        blackoutBG.color = new Color(1f - blackoutBG.color.r, 1f - blackoutBG.color.g, 1f - blackoutBG.color.b, blackoutBG.color.a);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void FadeOut(float transitionTime)
    {
        StartCoroutine(FadeOut_c(transitionTime));
    }

    public void FadeIn(float transitionTime)
    {
        StartCoroutine(FadeIn_c(transitionTime));
    }

    private IEnumerator FadeOut_c(float transitionTime)
    {
        blackoutBG.gameObject.SetActive(true);
        float elapsedTime = 0;
        float progress;
        blackoutBG.color = new Color(blackoutBG.color.r, blackoutBG.color.g, blackoutBG.color.b, 1);
        Color startingColor = blackoutBG.color;
        Color targetColor = startingColor;
        Color currentColor = startingColor;
        targetColor.a = 0;

        while (elapsedTime < transitionTime)
        {
            progress = Mathf.Clamp01(elapsedTime / transitionTime);
            currentColor.a = Mathf.Lerp(startingColor.a, targetColor.a, progress);
            blackoutBG.color = currentColor;
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        blackoutBG.gameObject.SetActive(false);

    }

    private IEnumerator FadeIn_c(float transitionTime)
    {
        blackoutBG.gameObject.SetActive(true);
        float elapsedTime = 0;
        float progress;
        blackoutBG.color = new Color(blackoutBG.color.r, blackoutBG.color.g, blackoutBG.color.b, 0);
        Color startingColor = blackoutBG.color;
        Color targetColor = startingColor;
        Color currentColor = startingColor;
        targetColor.a = 1;

        while (elapsedTime < transitionTime)
        {
            progress = Mathf.Clamp01(elapsedTime / transitionTime);
            currentColor.a = Mathf.Lerp(startingColor.a, targetColor.a, progress);
            blackoutBG.color = currentColor;
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }


    

    public void Epilepsy(int flashTimes)
    {
        StartCoroutine(Epilepsy_c(flashTimes));
    }

    private IEnumerator Epilepsy_c(int flashTimes)
    {
        WaitForSeconds epilepsyDelayWFS = new WaitForSeconds(epilepsyFlashDelay);
        for (int i = 0; i < flashTimes; i++)
        {
            blackoutBG.gameObject.SetActive(true);
            yield return epilepsyDelayWFS;
            blackoutBG.gameObject.SetActive(false);
            yield return epilepsyDelayWFS;
        }

        onEpilepsyEnd.Invoke();

    }

    public void ChangeVolumeProfile(VolumeProfile volumeProfile)
    {
        globalVolume.profile = volumeProfile;
    }

    public void SetFogColor(bool carousel)
    {
        if (carousel)
        {
            RenderSettings.fogColor = carouselFogColor;
        }
        else
        {
            RenderSettings.fogColor = defaultFogColor;
        }

    }

    public void SetFogDensity(float density)
    {
        RenderSettings.fogDensity = density;
    }


    public void LockMouse()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void UnlockMouse()
    {
        Cursor.lockState = CursorLockMode.Confined;
    }

    public void SetBGAlpha(float alpha)
    {
        blackoutBG.color = new Color(blackoutBG.color.r, blackoutBG.color.g, blackoutBG.color.b, alpha);
    }

    public void FadeOutBloom(float startIntensity)
    {
        StartCoroutine(FadeOutBloomCoroutine(startIntensity));
    }

    public void FadeInBloom(float targetIntensity)
    {
        StartCoroutine(FadeInBloomCoroutine(targetIntensity));
    }

    private IEnumerator FadeOutBloomCoroutine(float startIntensity)
    {
        // Получаем компонент Bloom из VolumeProfile
        if (globalVolume.profile.TryGet<Bloom>(out Bloom bloom))
        {
            // Сохраняем текущее значение как целевое (к которому будем фейдить)
            float targetIntensity = bloom.intensity.value;

            // Устанавливаем начальное значение
            bloom.intensity.value = startIntensity;

            float elapsedTime = 0;
            float progress;

            while (elapsedTime < bloomFadeTime)
            {
                progress = Mathf.Clamp01(elapsedTime / bloomFadeTime);
                bloom.intensity.value = Mathf.Lerp(startIntensity, targetIntensity, progress * progress);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            bloom.intensity.value = targetIntensity;
        }
        else
        {
            Debug.LogWarning("Bloom component not found in VolumeProfile!");
        }
    }

    private IEnumerator FadeInBloomCoroutine(float targetIntensity)
    {
        if (globalVolume.profile.TryGet<Bloom>(out Bloom bloom))
        {
            // Сохраняем текущее значение как стартовое
            float startIntensity = bloom.intensity.value;

            float elapsedTime = 0;
            float progress;

            while (elapsedTime < bloomFadeTime)
            {
                progress = Mathf.Clamp01(elapsedTime / bloomFadeTime);
                bloom.intensity.value = Mathf.Lerp(startIntensity, targetIntensity, progress * progress);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            bloom.intensity.value = targetIntensity;
        }
        else
        {
            Debug.LogWarning("Bloom component not found in VolumeProfile!");
        }
    }

    // Также можно добавить метод для установки мгновенного значения:
    public void SetBloomIntensity(float intensity)
    {
        if (globalVolume.profile.TryGet<Bloom>(out Bloom bloom))
        {
            bloom.intensity.value = intensity;
        }
    }

    // И метод для фейда с динамическим временем:
    public void FadeOutBloom(float startIntensity, float duration)
    {
        StartCoroutine(FadeOutBloomCoroutine(startIntensity, duration));
    }

    public void FadeInBloom(float targetIntensity, float duration)
    {
        StartCoroutine(FadeInBloomCoroutine(targetIntensity, duration));
    }

    private IEnumerator FadeOutBloomCoroutine(float startIntensity, float duration)
    {
        if (globalVolume.profile.TryGet<Bloom>(out Bloom bloom))
        {
            float targetIntensity = bloom.intensity.value;
            bloom.intensity.value = startIntensity;

            float elapsedTime = 0;
            float progress;

            while (elapsedTime < duration)
            {
                progress = Mathf.Clamp01(elapsedTime / duration);
                bloom.intensity.value = Mathf.Lerp(startIntensity, targetIntensity, progress * progress);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            bloom.intensity.value = targetIntensity;
        }
        else
        {
            Debug.LogWarning("Bloom component not found in VolumeProfile!");
        }
    }

    private IEnumerator FadeInBloomCoroutine(float targetIntensity, float duration)
    {
        if (globalVolume.profile.TryGet<Bloom>(out Bloom bloom))
        {
            float startIntensity = bloom.intensity.value;

            float elapsedTime = 0;
            float progress;

            while (elapsedTime < duration)
            {
                progress = Mathf.Clamp01(elapsedTime / duration);
                bloom.intensity.value = Mathf.Lerp(startIntensity, targetIntensity, progress * progress);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            bloom.intensity.value = targetIntensity;
        }
        else
        {
            Debug.LogWarning("Bloom component not found in VolumeProfile!");
        }
    }

}
