using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AngelSmokingAnim : MonoBehaviour
{
   public enum ModelState { Default, SmokingPrep, Smoked }

   private GameObject currentModel;
   [SerializeField] private GameObject defaultModel;
   [SerializeField] private GameObject smokingPrepModel;
   [SerializeField] private GameObject smokedModel;
   [SerializeField] private AnimationBase modelChangeAnim;

   [SerializeField] private float gapBetweenFrames=1f;
   [SerializeField] private GameObject smokeParticles;
   [SerializeField] private ParticleSystem continuousSmoking;

   [SerializeField] private bool smoking=true;
   [SerializeField] private float gapBetweenAnimations;
   private float gapBetweenAnimationsDistribution = 0.333f;
   
   private void Start()
   {
      ChangeModel(ModelState.Default);
      if (smoking) StartCoroutine(SmokingCoroutine());
   }

   private IEnumerator SmokingCoroutine()
   {
      yield return null;
      while (true)
      {
         yield return new WaitForSeconds(gapBetweenAnimations * h.RandMult(gapBetweenAnimationsDistribution));
         yield return SmokingAnimationCoroutine();
      }
   }

   private void ChangeModel(ModelState state)
   {
      defaultModel.SetActive(state == ModelState.Default);
      smokingPrepModel.SetActive(state == ModelState.SmokingPrep);
      smokedModel.SetActive(state == ModelState.Smoked);

      switch (state)
      {
         case ModelState.Default: currentModel = defaultModel; break;
         case ModelState.SmokingPrep: currentModel = smokingPrepModel; break;
         case ModelState.Smoked: currentModel = smokedModel; break;
      }
   }

   public IEnumerator SmokingAnimationCoroutine()
   {
      yield return null;
      if (continuousSmoking != null)
         continuousSmoking.Stop(true, ParticleSystemStopBehavior.StopEmitting);

      StartCoroutine(modelChangeAnim.Play());
      ChangeModel(ModelState.SmokingPrep);
      SFXManager.Instance.PlayRandomClip(new List<AudioClip>(
         ){R.PROJECT.Audio.smokesound},
         volumeIn:1f);
      yield return new WaitForSeconds(3.7f);

      StartCoroutine(modelChangeAnim.Play());
      ChangeModel(ModelState.Smoked);
      SpawnSmokeParticles();
      SFXManager.Instance.PlayRandomClip(new List<AudioClip>(
      ){R.PROJECT.Audio.exhalesound},
      volumeIn:1f);
      yield return new WaitForSeconds(4f);

      StartCoroutine(modelChangeAnim.Play());
      ChangeModel(ModelState.Default);

      if (continuousSmoking != null)
         continuousSmoking.Play();
   }

   private void SpawnSmokeParticles()
   {


      GameObject spawnPoint = h.FindChildWithTag(smokedModel.transform, "SpawnPoint");

      Instantiate(smokeParticles, spawnPoint.transform.position, spawnPoint.transform.rotation);
   }
}
