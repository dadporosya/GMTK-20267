using System;
using System.Collections;
using UnityEngine;

public class AngelSmokingAnim : MonoBehaviour
{
   private GameObject currentModel;
   [SerializeField] private GameObject defaultModel;
   [SerializeField] private GameObject smokingPrepModel;
   [SerializeField] private GameObject smokedModel;
   [SerializeField] private AnimationBase modelChangeAnim;

   [SerializeField] private float gapBetweenFrames=1f;
   [SerializeField] private GameObject smokeParticles;

   private void Start()
   {
      currentModel = defaultModel;
      defaultModel.SetActive(true);
      smokingPrepModel.SetActive(false);
      smokedModel.SetActive(false);
   }
   
   public void Update()
   {
      if (Input.GetKeyDown(KeyCode.E))
      {
         SmokeAnimation();
      }
   }

   public void SmokeAnimation()
   {
      StartCoroutine(SmokeAnimationCoroutine());
   }

   public IEnumerator SmokeAnimationCoroutine()
   {
      yield return null;
      StartCoroutine(modelChangeAnim.Play());
      defaultModel.SetActive(false);
      smokingPrepModel.SetActive(true);
      smokedModel.SetActive(false);
      yield return new WaitForSeconds(gapBetweenFrames);
      
      StartCoroutine(modelChangeAnim.Play());
      defaultModel.SetActive(false);
      smokingPrepModel.SetActive(false);
      smokedModel.SetActive(true);
      /// TASK find in smokedModel object with tag SpawnPoint, and spawn there smokeParticles
      /// in addition, make
      /// defaultModel.SetActive(true);
      /// smokingPrepModel.SetActive(false);
      /// smokedModel.SetActive(false);
      /// as a func ChangeModel(state)
      yield return new WaitForSeconds(gapBetweenFrames);
      
      StartCoroutine(modelChangeAnim.Play());
      defaultModel.SetActive(true);
      smokingPrepModel.SetActive(false);
      smokedModel.SetActive(false);
   }
}
