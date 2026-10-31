using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Inventory;
using Scrapfire.Characters.Weapon;
using Scrapfire.Pooling;
using Scrapfire.Audio;

namespace Scrapfire.Tests
{
    public class PresentationPoolTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        [TearDown]
        public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }
            _objects.Clear();
        }

        [Test]
        public void PoolSharesBucketsPrewarmsOnceAndNeverReusesAnActiveLease()
        {
            var pool = Create("testPool").AddComponent<SharedObjectPool>();
            var prefab = Create("pooledTemplate");
            prefab.SetActive(false);
            prefab.transform.localScale = Vector3.one * 0.3f;
            var otherPrefab = Create("otherTemplate");
            otherPrefab.SetActive(false);
            pool.Prewarm(prefab, 2);
            pool.Prewarm(prefab, 2);
            Assert.AreSame(pool, SharedObjectPool.ForScene(pool.gameObject.scene));
            Assert.AreEqual(2, pool.GetCreatedCount(prefab));
            PooledObject first = pool.Rent(prefab, Vector3.one, Quaternion.identity);
            PooledObject second = pool.Rent(prefab, Vector3.zero, Quaternion.identity);
            Assert.AreNotSame(first, second);
            PooledObject third = pool.Rent(prefab, Vector3.zero, Quaternion.identity);
            Assert.AreEqual(3, pool.GetCreatedCount(prefab));
            Assert.AreEqual(3, pool.GetActiveCount(prefab));
            first.transform.localScale = Vector3.one * 4f;
            first.Release();
            first.Release();
            Assert.AreEqual(2, pool.GetActiveCount(prefab));
            PooledObject reused = pool.Rent(prefab, Vector3.right, Quaternion.identity);
            Assert.AreSame(first, reused);
            Assert.AreEqual(Vector3.right, reused.transform.position);
            Assert.AreEqual(prefab.transform.localScale, reused.transform.localScale);
            Assert.AreEqual(3, pool.GetCreatedCount(prefab));
            PooledObject other = pool.Rent(otherPrefab, Vector3.zero, Quaternion.identity);
            Assert.AreNotSame(reused, other);
            Assert.AreEqual(1, pool.GetCreatedCount(otherPrefab));
        }

        [UnityTest]
        public IEnumerator MovingTraceAndEffectExpireAndResetOnReuse()
        {
            var pool = Create("visualPool").AddComponent<SharedObjectPool>();
            var prefab = Create("traceTemplate");
            prefab.SetActive(false);
            var lineTemplate = prefab.AddComponent<LineRenderer>();
            lineTemplate.widthMultiplier = 0.1f;
            var template = prefab.AddComponent<BulletTrace>();
            template.MinimumTravelTime = 0.1f;
            template.MaximumTravelTime = 0.1f;
            template.FadeTime = 0.02f;
            pool.Prewarm(prefab, 1);
            PooledObject first = pool.Rent(prefab, Vector3.zero, Quaternion.identity);
            first.GetComponent<BulletTrace>().Play(Vector3.zero, Vector3.forward * 10f);
            var line = first.GetComponent<LineRenderer>();
            Assert.Less(line.GetPosition(1).z, 10f);
            Assert.AreEqual(0, first.GetComponentsInChildren<Collider>().Length);
            yield return null;
            yield return null;
            Assert.Greater(line.GetPosition(1).z, 0.1f);
            Assert.AreSame(pool.transform, first.transform.parent);
            yield return new WaitForSeconds(0.2f);
            Assert.IsFalse(first.InUse);
            Assert.IsFalse(line.enabled);
            PooledObject reused = pool.Rent(prefab, Vector3.right * 100f, Quaternion.identity);
            Assert.AreSame(first, reused);
            reused.GetComponent<BulletTrace>().Play(Vector3.right * 100f, Vector3.right * 102f);
            Assert.AreEqual(0.1f, line.widthMultiplier);
            Assert.GreaterOrEqual(line.GetPosition(0).x, 100f);
            Assert.AreEqual(1, pool.GetCreatedCount(prefab));
            var effectPrefab = Create("effectTemplate");
            effectPrefab.SetActive(false);
            effectPrefab.AddComponent<PooledEffect>();
            var effect = pool.Rent(effectPrefab, Vector3.zero, Quaternion.identity);
            effect.GetComponent<PooledEffect>().Play(0.02f);
            yield return new WaitForSeconds(0.05f);
            Assert.IsFalse(effect.InUse);
            Assert.AreSame(effect, pool.Rent(effectPrefab, Vector3.one, Quaternion.identity));
        }

        [UnityTest]
        public IEnumerator ShotgunUsesOneAudioVoiceAndEightIndependentTracesThatSurviveSwitching()
        {
#if UNITY_EDITOR
            Item item = UnityEditor.AssetDatabase.LoadAssetAtPath<Item>("Assets/Scrapfire/Items/PrototypeShotgun.asset");
            Assert.IsNotNull(item);
            var pool = Create("weaponPresentationPool").AddComponent<SharedObjectPool>();
            var owner = Create("presentationOwner").AddComponent<CharacterPlayer>();
            owner.transform.position = Vector3.one * 25000f;
            owner.CharacterInventory.Add(new ItemStack(item));
            var gun = (RangedWeapon)owner.CurrentWeapon;
            var audio = gun.GetComponent<WeaponAudio>();
            var tracer = gun.GetComponent<ShotTracer>();
            Assert.IsNotNull(audio.FireClip);
            Assert.Greater(audio.FireClip.samples, 0);
            Assert.IsTrue(owner.TryAttack(Vector3.forward));
            Assert.AreEqual(8, pool.GetActiveCount(tracer.TracePrefab));
            Assert.AreEqual(1, pool.GetActiveCount(audio.VoicePrefab));
            var voice = pool.GetComponentsInChildren<PooledAudioVoice>()
                .Single(v => v.GetComponent<PooledObject>().InUse);
            Assert.AreSame(audio.FireClip, voice.GetComponent<AudioSource>().clip);
            owner.SelectWeapon(1);
            Assert.IsNull(owner.CurrentWeapon);
            Assert.IsTrue(voice.GetComponent<PooledObject>().InUse);
            Assert.AreEqual(8, pool.GetActiveCount(tracer.TracePrefab));
            voice.GetComponent<PooledObject>().Release();
            Assert.IsNull(voice.GetComponent<AudioSource>().clip);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0, pool.GetActiveCount(tracer.TracePrefab));
#else
            yield return null;
#endif
        }
    }
}
