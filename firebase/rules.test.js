// 게임이 하는 쓰기는 통과하고, 그 밖의 쓰기는 막히는지 확인. 실행: npm test (Java 21 필요)
import { test, before } from 'node:test';
import { readFileSync } from 'node:fs';
import { initializeTestEnvironment, assertSucceeds, assertFails } from '@firebase/rules-unit-testing';
import {
  doc, collection, getDoc, getDocs, setDoc, updateDoc, deleteDoc, query, where, orderBy, limit,
  getCountFromServer, increment, serverTimestamp,
} from 'firebase/firestore';

let env, db;
const user = (id) => doc(db, 'users', id);

before(async () => {
  env = await initializeTestEnvironment({
    projectId: 'demo-sch',
    firestore: { rules: readFileSync('firestore.rules', 'utf8'), host: '127.0.0.1', port: 8080 },
  });
});

async function seed() {
  await env.clearFirestore();
  await env.withSecurityRulesDisabled(async (c) => {
    const d = c.firestore();
    await setDoc(doc(d, 'users', '20240001'), { dept: '컴공', studentId: '20240001', name: '홍길동', bestScore: 500, plays: 2, playsUsed: 2, lastScore: 500 });
    await setDoc(doc(d, 'users', '20240002'), { dept: '컴공', studentId: '20240002', name: '김철수', bestScore: 0, plays: 3, playsUsed: 3 });
    await setDoc(doc(d, 'users', '20230001'), { dept: '경영', studentId: '20230001', name: '이영희', bestScore: 100, plays: 5 }); // playsUsed 없는 예전 문서
  });
  db = env.unauthenticatedContext().firestore();
}

const newUser = (id) => ({ dept: '컴공', studentId: id, name: '박민수', bestScore: 0, plays: 0, playsUsed: 0, createdAt: serverTimestamp() });
const result = (score) => ({ plays: increment(1), lastScore: score, updatedAt: serverTimestamp() });
const best = (score) => ({ ...result(score), bestScore: score, bestMeters: 300, bestKills: 10, bestHits: 1 });

test('게임이 하는 동작은 통과', async () => {
  await seed();
  await assertSucceeds(getDoc(user('20240001')));
  const ranked = query(collection(db, 'users'), where('bestScore', '>', 0), orderBy('bestScore', 'desc'), limit(10));
  await assertSucceeds(getDocs(ranked));
  await assertSucceeds(getCountFromServer(query(collection(db, 'users'), where('bestScore', '>', 0))));
  await assertSucceeds(setDoc(user('20249999'), newUser('20249999')));
  await assertSucceeds(updateDoc(user('20240001'), { dept: '전자' }));
  await assertSucceeds(updateDoc(user('20240001'), { playsUsed: increment(1) }));
  await assertSucceeds(updateDoc(user('20230001'), { playsUsed: increment(1) }));
  await assertSucceeds(setDoc(user('20240001'), result(300), { merge: true }));
  await assertSucceeds(setDoc(user('20240001'), best(900), { merge: true }));
  await assertSucceeds(setDoc(user('20230001'), best(200), { merge: true }));
});

test('그 밖의 쓰기는 막힘', async () => {
  await seed();
  await assertFails(deleteDoc(user('20240001')));
  await assertFails(setDoc(user('20249999'), { ...newUser('20249999'), bestScore: 999 }));
  await assertFails(setDoc(user('20249999'), { ...newUser('20249999'), admin: true }));
  await assertFails(setDoc(user('20249999'), newUser('20240001')));
  await assertFails(setDoc(user('abc'), newUser('abc')));
  await assertFails(setDoc(user('20240001'), newUser('20240001'))); // 기존 학번 덮어쓰기
  await assertFails(updateDoc(user('20240001'), { name: '해커' }));
  await assertFails(updateDoc(user('20240001'), { playsUsed: 0 }));
  await assertFails(updateDoc(user('20240001'), { playsUsed: increment(2) }));
  await assertFails(updateDoc(user('20240002'), { playsUsed: increment(1) })); // 이미 3판
  await assertFails(setDoc(user('20240001'), best(400), { merge: true })); // 최고 기록보다 낮음
  await assertFails(setDoc(user('20240001'), best(99999999), { merge: true }));
  await assertFails(setDoc(user('20240001'), { ...best(900), bestScore: 1000 }, { merge: true }));
  await assertFails(setDoc(user('20240001'), { bestScore: 900 }, { merge: true })); // 판 수 안 올리고 점수만
  await assertFails(setDoc(doc(db, 'other', 'x'), { a: 1 }));
});
