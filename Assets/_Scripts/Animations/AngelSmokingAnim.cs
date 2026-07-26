using System;
using System.Collections;
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
   
   public void Update()
   {
      if (Input.GetKeyDown(KeyCode.E))
      {
         StartCoroutine(SmokingAnimationCoroutine());
      }
   }

   public IEnumerator SmokingAnimationCoroutine()
   {
      yield return null;
      if (continuousSmoking != null)
         continuousSmoking.Stop(true, ParticleSystemStopBehavior.StopEmitting);

      StartCoroutine(modelChangeAnim.Play());
      ChangeModel(ModelState.SmokingPrep);
      yield return new WaitForSeconds(gapBetweenFrames);

      StartCoroutine(modelChangeAnim.Play());
      ChangeModel(ModelState.Smoked);
      SpawnSmokeParticles();
      yield return new WaitForSeconds(gapBetweenFrames*2);

      StartCoroutine(modelChangeAnim.Play());
      ChangeModel(ModelState.Default);

      if (continuousSmoking != null)
         continuousSmoking.Play();
   }

   private void SpawnSmokeParticles()
   {
      if (smokeParticles == null) return;

      GameObject spawnPoint = h.FindChildWithTag(smokedModel.transform, "SpawnPoint");
      if (spawnPoint == null)
      {
         h.Out("AngelSmokingAnim: no child tagged 'SpawnPoint' found in smokedModel");
         return;
      }

      Instantiate(smokeParticles, spawnPoint.transform.position, spawnPoint.transform.rotation);
   }
}
