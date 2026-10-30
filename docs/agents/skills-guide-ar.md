# دليل استخدام مهارات Matt Pocock مع كل تيكت

دليل عملي بالعربي. التفاصيل الكاملة موجودة في [`skills-workflow.md`](skills-workflow.md)، والـ roadmap
([`ROADMAP.md`](../../ROADMAP.md)) هو اللي بيحدد **إيه** اللي بيتعمل. الملف ده بيقولك **إزاي** تشغّل كل حاجة.

بتكتب المهارة في Claude Code كده: `/mattpocock-skills:<name>` أو `/<name>`.

---

## 1. القرار الأول: التيكت ده محتاج spec ولا لأ؟

اسأل نفسك الأسئلة دي بالترتيب:

```
هل فيه design مقبول (specs/...) والتيكت متقطّع منه وفيه "Tests to write first"؟
├── أيوه  → التيكت جاهز. روح على  (B) تيكت جاهز
└── لأ
    ├── هل فيه قرار بيزنس أو تصميم لسه مش محسوم (حالة جديدة، قاعدة جديدة، مين يقدر يعمل إيه)؟
    │   ├── أيوه → (A) محتاج spec
    │   └── لأ
    │       ├── هو bug؟                         → (C) bug
    │       ├── تغيير صغير واضح (سطر، config)؟   → (D) تغيير صغير
    │       └── مش عارف                         → /ask-matt
```

**علامات إن التيكت محتاج spec:**
- فيه كلمة جديدة مش موجودة في [`CONTEXT.md`](../../CONTEXT.md).
- فيه حالة (state) جديدة أو transition جديد.
- مش عارف تكتب أسماء التستات قبل ما تبدأ.
- التيكت بيلمس أكتر من role (Customer + Delivery Agent + Admin) بطريقة جديدة.
- لو اتنين مبرمجين قروا التيكت، هيبنوه بطريقتين مختلفتين.

**تيكتات Phase 2 الحالية (T3 لحد T9) مش محتاجة spec.** اتقطّعت من
[`specs/011-order-lifecycle/design.md`](../../specs/011-order-lifecycle/design.md)، وكل واحد فيها مكتوب فيه التستات
اللي تتكتب الأول. فهي كلها **(B)**.

---

## (A) تيكت محتاج spec: الطريق الكامل

خطوات 1 لحد 3 تتعمل في **نفس الجلسة**. ماتعملش `/clear` قبل ما التيكتات تطلع.

| الخطوة | المهارة | بتعمل إيه | تخلص إمتى |
|---|---|---|---|
| 1 | `/grill-with-docs` | Claude يسألك أسئلة، جولة جولة، لحد ما كل قرار يتحسم. وهو شغال بيحدّث `CONTEXT.md` ويكتب ADRs في `docs/adr/`. | مفيش سؤال مفتوح |
| 1a | `/prototype` *(اختياري)* | نموذج سريع يترمي بعد كده، لما سؤال ميتحسمش غير بكود شغال. | السؤال اتجاوب |
| 1b | `/research` *(اختياري)* | يجيب حقايق من مصادر رسمية (EF Core، Duende…) في ملف Markdown. | الحقيقة بقت معروفة |
| 2 | `/to-spec` | يكتب الـ design في `specs/<nnn>-<slug>/` وينشره على GitHub. | **إنت** توافق على الـ spec |
| 3 | `/to-tickets` | يقطّع الـ spec لتيكتات رأسية (T0، T1…) كـ GitHub issues، ويحدد مين blocking مين، ويعمل برانش `feature/phase-<n>-<slug>`. | التيكتات اتعملت |
| 4 | لكل تيكت → **(B)** | | |

التيكتات اللي بتطلع من `/to-tickets` جاهزة على طول، **فماتشغّلش عليها `/triage`.**

لو الشغل ضخم وأكبر من جلسة واحدة (مثلًا Phase 6 أو Phase 11)، ابدأ بـ `/wayfinder`. بيعمل خريطة من تيكتات قرارات
تحسمها واحدة واحدة، وبعدها تكمّل بالجدول اللي فوق.

---

## (B) تيكت جاهز: بناء تيكت واحد

**قاعدة:** تيكت واحد في كل جلسة، بالترتيب، والـ blockers الأول.

### الخطوات

1. **`/clear`**: ابدأ بـ context نضيف، لأن التيكت فيه كل اللي محتاجه.
2. **اتأكد إنك على برانش الـ phase ومتحدّث:** `feature/phase-2-order-lifecycle`.
3. **`/tdd for T<n>`**: Claude هيقرأ التيكت والـ design، ويقترح عليك **الـ seams**، يعني الأماكن اللي التستات هتتكتب
   عندها، زي الـ handler بـ fakes أو SQL Server أو HTTP. **وافق عليها أو عدّلها.** ده أهم قرار بتاخده في التيكت.
4. **`/implement T<n> …`**: بعد الموافقة على الـ seams، بيعمل بالترتيب:
   - برانش للتيكت من برانش الـ phase (`feat/t<n>-<slug>`)؛
   - لكل seam: تستات **red** في commit، بعدها كود **green** في commit؛
   - الـ OpenAPI contract لو اتغيّر بيتعمله regenerate في **commit لوحده**؛
   - الـ test suite كلها مرة واحدة في الآخر؛
   - **`/code-review`** (Standards + Spec) وبيعرض عليك اللي لقاه.
5. **قرّر في نتايج الـ review:** قول أنهي fixes تتعمل (A، B، C…). كل fix بيتعمل test-first.
6. **ملف الشرح التعليمي (إلزامي):** `docs/learning/T<n>-technical-walkthrough.md` حسب
   [`docs/learning/README.md`](../learning/README.md). لازم يتعمل **قبل** الـ PR.
7. **الـ PR** على برانش الـ phase، وبعدها شغّل **Auto-fix** على الـ CI من التطبيق.
8. **الـ merge** لما الـ CI يبقى أخضر، وبعدها امسح برانش التيكت.

### جملة جاهزة تنسخها (مثال T3)

```
/mattpocock-skills:tdd for T3
```

وبعد ما توافق على الـ seams:

```
/mattpocock-skills:implement T3 — engakhattab/Talabat_API#12. Seams approved: <الـ seams اللي وافقت عليها>. Branch feat/t3-<slug> → PR into feature/phase-2-order-lifecycle.
```

---

## (C) bug

| الحالة | المهارة |
|---|---|
| bug واضح وسببه معروف | `/tdd`: تست بيفشل على الـ bug (red)، بعده الإصلاح (green) |
| bug مش واضح، أو flaky test، أو حاجة كانت شغالة وبطّلت | `/diagnosing-bugs`: يعمل أمر بيفشل على الـ bug الأول، ويخلص بـ regression test |

وبرضه: ملف شرح تعليمي قبل الـ PR، و`/code-review` لو التغيير مش صغير.

## (D) تغيير صغير

`/tdd` على طول (أو تعديل مباشر لو مفيش behaviour، زي docs أو config)، وبعده ملف شرح قصير (القسمين 1 و3 بس) والـ PR.

---

## 2. مهارات بتحتاجها وسط الشغل

| الموقف | المهارة |
|---|---|
| issues جت من حد تاني، أو طلبات متراكمة | `/triage` |
| merge أو rebase وقف على conflicts | `/resolving-merge-conflicts` |
| كلمة في الكود أو الدوكس معناها مش واضح، أو مصطلح جديد | `/domain-modeling` |
| عاوز تصمم شكل module أو interface | `/codebase-design` |
| خطوة إنت بس اللي تقدر تعملها (secrets، إعدادات CI، staging) | `/wizard` |
| شرح ما وصلّكش | `/wait-what` |
| عاوز تتعلم مفهوم جديد جوه المشروع | `/teach` |
| بعد تيكت أو phase، عاوز تراجع إيه اللي مشي كويس وإيه لأ | `/retro` |
| وقت فاضي بين الـ phases | `/improve-codebase-architecture` |
| بتعدّل `CLAUDE.md` أو `AGENTS.md` أو `docs/agents/` | `/writing-for-agents` |
| الشغل هيتنقل لريبو تاني (الباك إند → الفرونت إند في Phase 3) | `/handoff` |
| **مش عارف أنهي مهارة** | **`/ask-matt`** |

## 3. إدارة الجلسة

- **كمّل في نفس الجلسة** لو الخطوة الجاية محتاجة اللي في الـ context، زي grill → spec → tickets.
- **`/clear`** بين كل تيكت والتاني.
- **`/compact`** لو الجلسة طولت والتفكير اللي فيها لسه مهم.
- **`/handoff`** لما تنقل الشغل لريبو تاني.

## 4. checklist قبل ما تقفل أي تيكت

- [ ] كل التستات المكتوبة في التيكت اتكتبت **red** الأول، والكود بعدها **green**.
- [ ] الـ test suite كلها خضرا (`dotnet test src/Talabat/Talabat.slnx`).
- [ ] الـ contracts اللي اتغيرت ليها commit لوحدها، والـ parity tests خضرا.
- [ ] نتايج `/code-review` اتحسمت: اتصلّحت أو اتأجلت بسبب.
- [ ] `docs/learning/T<n>-technical-walkthrough.md` اتكتب.
- [ ] الـ PR على برانش الـ phase، والـ CI أخضر، والـ merge اتعمل، وبرانش التيكت اتمسح.
- [ ] كل رسالة فيها الحالة: الـ milestone/phase، اللي خلص، واللي جاي.
