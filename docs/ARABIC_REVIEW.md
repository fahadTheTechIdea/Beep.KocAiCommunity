# Arabic review sheet

**Every Arabic string in this file was written by Claude, not by a native speaker.** It is here to be
corrected, not approved. Nothing about the platform depends on this review to keep working — an
untranslated or badly translated string still renders, so this can be worked through a screen at a
time without blocking anything.

## What has already been checked, mechanically

These are properties of the build, enforced by `LocalizationCoverageTests`, so a reviewer does not
need to look for them:

- **Coverage** — every `L["..."]` in the markup has an Arabic entry. A missing one fails the build.
- **No orphans** — no translation survives whose English text has been edited away.
- **No empty values** — an empty translation renders as nothing at all, which is worse than English.
- **No case-collisions** — `.resx` names are case-insensitive, so `Score` and `score` cannot coexist.
- **Placeholders match** — `{0}`, `{1}` in the Arabic line up with the English. One deliberate
  exception: `{0} reply` is rendered `رد واحد`, because the call site only uses it when the count is
  one and Arabic does not want the numeral there.
- **Terminology is internally consistent** — a term-by-term pass found no concept using two different
  Arabic words. The English distinction between *leaderboard* (لوحة الصدارة) and *standings*
  (الترتيب) is carried through deliberately, as is *send* (إرسال) versus *submission* (مشاركة).

## What still needs a person

1. **Whether the words are the ones KOC actually uses.** This is the whole point of the review.
   The one term already corrected this way — the site name, now
   منصة مجموعة التدريب والتطوير الوظيفي - شركة نفط الكويت — is the example: it was defensible
   Arabic and still the wrong name.
2. **Register.** Much of this is instructional copy aimed at colleagues. Technically correct and
   still too stiff, or too casual, is not something a machine check finds.
3. **Plural forms.** Arabic distinguishes singular, dual, and plural; the code only branches on
   one-versus-many, which is the shape English needs. So a count of two reads as
   "2 ردود" where it should read "ردان". Fixing it properly means plural-aware resources, which is
   a change to the code and not to this file — flagging it here so it is a known gap rather than a
   surprise.
4. **Terms deliberately left in Latin.** Person names, and the KOC logo wordmark.

## How to use this

Grouped by the screen the string appears on, so it can be reviewed against the running page rather
than as a list. Correct the Arabic column and hand it back; the strings are keyed by their English
text, so a correction is applied by editing the value against that key in
`src/Beep.KocAiCommunity.Ui.Shared/Localization/Strings.ar.resx`.

*758 strings across 54 screens.*

## AggregateBuilder

| English | Arabic | Also on |
|---|---|---|
| Column | العمود | DatasetVersionsDialog, Datasets, SortKeyBuilder, SqlConditionBuilder |
| Function | الدالة | — |
| Name (opt) | الاسم (اختياري) | — |

## AuditTable

| English | Arabic | Also on |
|---|---|---|
| Action | الإجراء | — |
| Actor | الفاعل | — |
| No audit events yet. | لا أحداث تدقيق بعد. | — |
| Resource | المورد | — |

## AutoMl

| English | Arabic | Also on |
|---|---|---|
| Algorithm | الخوارزمية | Experiments, Models, Runs |
| Best so far | الأفضل حتى الآن | — |
| Choose a dataset and the column you want to predict. Several algorithms are tried and the best one wins. Everything runs on this machine. | اختر مجموعة بيانات والعمود الذي تريد التنبؤ به. تُجرَّب عدة خوارزميات ويفوز أفضلها. كل شيء يعمل على هذا الجهاز. | — |
| Column to predict | العمود المراد التنبؤ به | — |
| Dataset | مجموعة البيانات | Models, Runs |
| Done — {0} {1} | تمّ — {0} {1} | — |
| Finished | انتهى | Runs |
| Forecasting and anomaly detection need a chronological or unsupervised step — build those in the designer. | التنبؤ الزمني ورصد الحالات غير الطبيعية يحتاجان إلى تقسيم زمني أو خطوة غير خاضعة للإشراف — ابنِهما في المصمّم. | — |
| Go to datasets | انتقل إلى مجموعات البيانات | — |
| Import a CSV first — training needs something to learn from. | استورد ملف CSV أولًا — التدريب يحتاج إلى بيانات يتعلّم منها. | — |
| Kind of prediction | نوع التنبؤ | Models, Runs |
| No datasets yet | لا مجموعات بيانات بعد | Datasets |
| Not started | لم يبدأ | Dashboard, Home |
| Open this run | افتح هذا التشغيل | — |
| Progress appears here once a run starts. | يظهر التقدّم هنا بمجرد بدء التشغيل. | — |
| Run history | سجل التشغيلات | Models, Runs |
| Score | النتيجة | CompetitionDetail, Experiments, Leaderboards, LiveBoard, Runs |
| Seconds | الثواني | — |
| Start training | ابدأ التدريب | — |
| Stop | إيقاف | — |
| Stopped at the memory limit. Raise it in Settings, or train on fewer columns. | تمّ الإيقاف عند حدّ الذاكرة. ارفع الحدّ من الإعدادات، أو درّب على عدد أقل من الأعمدة. | — |
| Stopped. The attempts so far are saved in the run history. | تمّ الإيقاف. المحاولات حتى الآن محفوظة في سجل التشغيلات. | — |
| Stopping. The attempts below are kept; no model is saved from a run that was stopped. | جارٍ الإيقاف. المحاولات أدناه محفوظة؛ ولا يُحفظ نموذج من تشغيل تمّ إيقافه. | — |
| Stopping… | جارٍ الإيقاف… | — |
| Stops after {0} minutes or {1} MB of memory, whichever comes first. Change this in Settings. | يتوقّف بعد {0} دقيقة أو عند {1} ميجابايت من الذاكرة، أيهما أسبق. غيّر ذلك في الإعدادات. | — |
| Train a model | درِّب نموذجًا | Runs |
| Training | جارٍ التدريب | — |
| Training could not start: {0} | تعذّر بدء التدريب: {0} | — |
| Training failed: {0} | فشل التدريب: {0} | — |
| {0} attempts | {0} محاولة | Outbox, Runs |
| {0}s elapsed · {1} MB | مضى {0} ث · {1} ميجابايت | — |

## Community

| English | Arabic | Also on |
|---|---|---|
| Activity | النشاط | — |
| Ask, answer, and cheer each other on. Everything here stays inside KOC. | اسأل وأجب وشجّع زملاءك. كل ما هنا يبقى داخل شركة نفط الكويت. | — |
| Attach a file | إرفاق ملف | — |
| Attached {0}. | أُرفق {0}. | — |
| Attachments | المرفقات | — |
| Badges | الأوسمة | — |
| badges earned | وسام مكتسب | — |
| badges to earn | وسام يمكن كسبه | — |
| Community | المجتمع | Dashboard, TopNav |
| Couldn't reach the API ({0}). | تعذّر الوصول إلى الواجهة البرمجية ({0}). | Compete, Leaderboards |
| Delete thread | حذف النقاش | — |
| Discussions | النقاشات | — |
| Earned {0} | مُكتسب في {0} | — |
| join the conversation | المشاركة في النقاش | — |
| just now | الآن | — |
| Kudos | تحية | — |
| Lock | إقفال | — |
| Locked | مقفل | — |
| Mention | إشارة | — |
| New discussion | نقاش جديد | — |
| No discussions yet — share a question, a tip, or a win. | لا نقاشات بعد — شارك سؤالًا أو نصيحة أو إنجازًا. | — |
| Nothing here yet. Earn a badge or cheer a colleague to get things moving. | لا شيء هنا بعد. اكسب وسامًا أو شجّع زميلًا لتبدأ الحركة. | — |
| open discussions | نقاش مفتوح | — |
| Pin | تثبيت | — |
| See the standings | اعرض الترتيب | Compete |
| Select a discussion to read and reply. | اختر نقاشًا لقراءته والرد عليه. | — |
| Send | إرسال | — |
| Sign in | تسجيل الدخول | Login, Register |
| Start the conversation | ابدأ النقاش | — |
| The KOC AI community | مجتمع الذكاء الاصطناعي في KOC | — |
| This discussion is locked — no new replies. | هذا النقاش مقفل — لا ردود جديدة. | — |
| to join the conversation. | للمشاركة في النقاش. | — |
| Unlock | فتح | — |
| Unpin | إلغاء التثبيت | — |
| Write a reply | اكتب ردًا | — |
| You've earned {0} of {1} badges. Keep learning, competing, and helping colleagues to collect them all. | حصلت على {0} من {1} وسامًا. واصل التعلُّم والتنافس ومساعدة الزملاء لتجمعها كلها. | — |
| your Barrels | براميلك | — |
| {0} badges to collect — by learning, competing, and helping colleagues. | {0} وسامًا يمكن جمعها — بالتعلُّم والتنافس ومساعدة الزملاء. | — |
| {0} replies | {0} رد | — |
| {0} reply | رد واحد | — |
| {0}d ago | قبل {0} يوم | — |
| {0}h ago | قبل {0} ساعة | — |
| {0}m ago | قبل {0} دقيقة | — |

## Compete

| English | Arabic | Also on |
|---|---|---|
| All | الكل | Leaderboards |
| All domains | كل المجالات | — |
| Anomaly detection | رصد الحالات غير الطبيعية | CompetitionDetail |
| Any task | أي مهمة | — |
| Ask an admin to seed the demo data, or host a competition above. | اطلب من المشرف تحميل البيانات التجريبية، أو استضِف مسابقة من الأعلى. | — |
| Classification | تصنيف | — |
| Clear filters | مسح المرشّحات | Learn |
| Compete | نافس | DesktopLayout, TopNav |
| Competitions | المسابقات | Leaderboards |
| Domain | المجال | — |
| entries | مشاركات | — |
| Forecasting | تنبؤ زمني | — |
| Host a competition | استضِف مسابقة | CreateCompetitionDialog |
| Internal, Kaggle-style challenges on real KOC problems — get the data, build your model in KOC Studio on the desktop, and climb the live leaderboard. Final standings stay hidden until reveal day. | تحديات داخلية على غرار Kaggle حول مشكلات حقيقية في شركة نفط الكويت — احصل على البيانات، وابنِ نموذجك في استوديو KOC على سطح المكتب، وتسلّق لوحة الصدارة الحيّة. تبقى الترتيبات النهائية مخفية حتى يوم الكشف. | — |
| Multiclass | تصنيف متعدد | — |
| No competition matches these filters. Widen them and something will turn up. | لا توجد مسابقة تطابق هذه المرشّحات. وسّعها وستظهر نتائج. | — |
| No competitions yet | لا مسابقات بعد | — |
| Nothing here yet | لا شيء هنا بعد | Learn |
| Regression | انحدار | CompetitionDetail |
| running now | تجري الآن | — |
| Search competitions | ابحث في المسابقات | — |
| submissions | مشاركة | CompetitionCard, CompetitionDetail, Home, Leaderboards |
| The arena | الحلبة | — |
| until the next reveal | حتى الإعلان القادم | — |
| {0} competitions | {0} مسابقة | — |
| {0} of {1} competitions | {0} من {1} مسابقة | — |

## CompetitionCard

| English | Arabic | Also on |
|---|---|---|
| competitors | متنافس | CompetitionDetail, Home |
| data ready | البيانات جاهزة | — |
| Enter | ادخل | — |
| Hosted by {0} | يستضيفها {0} | CompetitionDetail, Leaderboards |
| Progress toward the final reveal | ما تبقّى حتى الكشف النهائي | CompetitionDetail, Home |

## CompetitionDetail

| English | Arabic | Also on |
|---|---|---|
|  · {0} {1} the leader ({2}). |  · {0} {1} المتصدّر ({2}). | — |
|  — you're leading the board. |  — أنت في صدارة اللوحة. | — |
| A banner for this competition — and the landing-page hero if it's featured. PNG or JPG (max 5 MB). | لافتة لهذه المسابقة — وصورة الصفحة الرئيسية إن كانت مختارة. PNG أو JPG (بحد أقصى 5 ميغابايت). | — |
| Activate — open submissions | فعِّل — افتح باب المشاركة | — |
| Answer key | مفتاح الإجابات | — |
| Attach the files in the Host tab. | أرفِق الملفات من تبويب الاستضافة. | — |
| Back to draft | إعادة إلى مسودة | — |
| Back to the arena | العودة إلى الحلبة | — |
| behind | خلف | — |
| best {0} | أفضل نتيجة {0} | — |
| Binary classification | تصنيف ثنائي | RunWorkflowDialog |
| Build your model | ابنِ نموذجك | — |
| Build your model however you like — KOC Studio on the desktop, or your own tools. Only your predictions come back here. | ابنِ نموذجك بالطريقة التي تناسبك — استوديو KOC على سطح المكتب، أو أدواتك الخاصة. لا يعود إلى هنا سوى تنبؤاتك. | — |
| Build your model in KOC Studio on the desktop, then submit your predictions below. | ابنِ نموذجك في KOC Studio على سطح المكتب، ثم أرسل تنبؤاتك أدناه. | — |
| Clear | مسح | Runs |
| Competition | مسابقة | CreateWorkflowDialog, Dashboard |
| Competition data | بيانات المسابقة | — |
| Competition not found | لم يُعثر على المسابقة | — |
| Competitors download the training data, build a model, and are scored on the evaluation set against your hidden answer key. | ينزّل المتنافسون بيانات التدريب، ويبنون نموذجًا، ويُقيَّمون على مجموعة التقييم مقابل مفتاح إجاباتك المخفي. | — |
| Conclude — close submissions | أنهِ — أغلق باب المشاركة | — |
| CSV of | ملف CSV يحتوي | — |
| Data | البيانات | — |
| Download | تنزيل | — |
| Download the labelled training set and the evaluation set from the Data tab. | نزِّل مجموعة التدريب المُصنَّفة ومجموعة التقييم من تبويب البيانات. | — |
| entries/day | مشاركة/يوم | — |
| Evaluation | التقييم | — |
| Evaluation set | مجموعة التقييم | — |
| Every entry | كل مشاركة | — |
| Final standings | الترتيب النهائي | — |
| Final standings revealed {0} | يُكشف الترتيب النهائي في {0} | — |
| Get the data | احصل على البيانات | — |
| Hero image | صورة الغلاف | CreateCompetitionDialog |
| Hidden answer key | مفتاح الإجابات المخفي | — |
| History | السجل | — |
| Host | الاستضافة | — |
| How it works | كيف تسير | — |
| Id column | عمود المعرّف | — |
| image set | تم ضبط الصورة | — |
| It may have been removed, or the link is out of date. | ربما حُذفت، أو أن الرابط قديم. | — |
| kept Restricted; never exposed. | يبقى مقيَّدًا؛ ولا يُكشف أبدًا. | — |
| KOC Studio reads these files directly — download them only if you want to explore them yourself. | يقرأ استوديو KOC هذه الملفات مباشرة — نزّلها فقط إن أردت استكشافها بنفسك. | — |
| Label column | عمود التصنيف | — |
| Leaderboard | لوحة الصدارة | XpBoard |
| Lifecycle | دورة الحياة | — |
| Live leaderboard | لوحة الصدارة المباشرة | — |
| Multiclass classification | تصنيف متعدد الفئات | RunWorkflowDialog |
| My submissions | مشاركاتي | — |
| New to this? | جديد على هذا؟ | — |
| No data files attached yet — this challenge currently accepts uploaded prediction files only. | لا توجد ملفات بيانات مرفقة بعد — تقبل هذه المسابقة حاليًا ملفات التنبؤ المرفوعة فقط. | — |
| no label; what they predict. | بلا تصنيف؛ وهو ما سيتنبأون به. | — |
| No submissions yet — your first upload puts you on the board. | لا مشاركات بعد — أول رفع يضعك على اللوحة. | — |
| off | عن | — |
| Overview | نظرة عامة | MainLayout |
| public to competitors. | متاحة للمتنافسين. | — |
| Publish training + evaluation data | انشر بيانات التدريب والتقييم | — |
| ready | جاهز | — |
| Reveal date | تاريخ الكشف | — |
| scored instantly against the hidden answer key. | يُقيَّم فورًا مقابل مفتاح الإجابات المخفي. | — |
| Scored on a hidden answer key shared by everyone — only modelling moves your rank. | يُقيَّم على مفتاح إجابات مخفي مشترك للجميع — لا يرفع ترتيبك سوى جودة النمذجة. | — |
| Scored: {0} (+{1} bbl) — your best result counts on the leaderboard. | النتيجة: {0} (+{1} برميل) — أفضل نتائجك هي المحتسبة على لوحة الصدارة. | — |
| Set reveal | حدّد الكشف | — |
| Start with the recommended learning track | ابدأ بالمسار التعليمي المقترح | — |
| Status | الحالة | Runs |
| Submissions are closed — this challenge has concluded. | أُغلق باب المشاركة — انتهت هذه المسابقة. | — |
| Submit & climb | أرسِل وارتقِ | — |
| Submit a prediction | أرسِل تنبؤًا | — |
| Submit your predictions | أرسل تنبؤاتك | — |
| Task | المهمة | — |
| The live leaderboard reflects your best score. | تعكس لوحة الصدارة المباشرة أفضل نتيجة لك. | — |
| The live leaderboard shows public scores; final private standings unlock on reveal day so no one can game the last hours. | تعرض لوحة الصدارة المباشرة النتائج العلنية؛ ويُكشف الترتيب النهائي الخاص يوم الكشف حتى لا يستغل أحد الساعات الأخيرة. | — |
| The {0} badge + {1} bbl on conclusion. | وسام {0} + {1} برميل عند انتهاء المسابقة. | — |
| The {0} gold badge + {1} bbl. | وسام {0} الذهبي + {1} برميل. | — |
| Time-series forecasting | تنبؤ بالسلاسل الزمنية | — |
| Timeline | الجدول الزمني | — |
| Top 3 | أفضل ثلاثة | — |
| Training data | بيانات التدريب | — |
| Until the reveal, the final private standings stay concealed so no one games the last day. | حتى موعد الكشف يبقى الترتيب النهائي الخاص محجوبًا حتى لا يستغل أحد اليوم الأخير. | — |
| Use KOC Studio on the desktop, or any tool you prefer — the leaderboard only sees your predictions. | استخدم استوديو KOC على سطح المكتب، أو أي أداة تفضّلها — لا ترى لوحة الصدارة سوى تنبؤاتك. | — |
| What you can win | ما يمكن أن تربحه | — |
| When | الوقت | — |
| Winner | الفائز | — |
| you predict | وأنت تتنبأ بـ | — |
| You're #{0} of {1} | ترتيبك {0} من {1} | — |
| You're not on the board yet — {0} competitor(s) ahead of you. | لست على اللوحة بعد — {0} متنافس أمامك. | — |
| {0} bbl per scored submission; your first ever earns the {1} badge (+{2} bbl). | {0} برميل لكل مشاركة مُقيَّمة؛ وأول مشاركة لك تمنحك وسام {1} (+{2} برميل). | — |
| {0} rows | {0} صف | — |

## ConfirmImportDialog

| English | Arabic | Also on |
|---|---|---|
| and {0} more columns | و{0} أعمدة أخرى | Datasets |
| Arabic (Windows-1256) | عربي (Windows-1256) | — |
| Cancel | إلغاء | CreateCompetitionDialog, CreateDatasetDialog, CreateDiscussionDialog, CreateWorkflowDialog, Datasets, GiveKudosDialog, Models, NameDialog, Outbox, RegisterModelDialog, RunWorkflowDialog, Runs |
| Check the columns below. If they look wrong, change the separator or the encoding. | راجع الأعمدة أدناه. إن بدت خاطئة، غيّر الفاصل أو الترميز. | — |
| comma | فاصلة | — |
| Encoding | الترميز | — |
| Import | استيراد | DatasetVersionsDialog, Models, Workflows |
| Nothing readable at these settings. | لا شيء مقروء بهذه الإعدادات. | — |
| pipe | شرطة رأسية | — |
| semicolon | فاصلة منقوطة | — |
| Separator | الفاصل | — |
| tab | مسافة جدولة | — |
| The copy kept here is saved as UTF-8 with commas. Your original file is not changed. | النسخة المحفوظة هنا تُحفظ بترميز UTF-8 وبفواصل. ملفك الأصلي لا يتغيّر. | — |
| The separator could not be worked out from the file. Check the columns carefully. | تعذّر استنتاج الفاصل من الملف. راجع الأعمدة بعناية. | — |
| Unicode (UTF-8) | يونيكود (UTF-8) | — |
| Windows default | إعداد ويندوز الافتراضي | — |
| {0} columns found | عُثر على {0} أعمدة | — |

## Connectors

| English | Arabic | Also on |
|---|---|---|
| Connectors | الموصلات | MainLayout |
| Enterprise connectors | موصلات المؤسسة | — |

## CountdownTimer

| English | Arabic | Also on |
|---|---|---|
| d | ي | — |
| days | يوم | — |
| Final reveal | الكشف النهائي | — |
| h | س | — |
| hrs | ساعة | — |
| m | د | — |
| min | دقيقة | — |
| Revealed | انكشفت النتائج | — |
| s | ث | — |
| sec | ثانية | — |

## CreateCompetitionDialog

| English | Arabic | Also on |
|---|---|---|
| <b>Binary</b>: two outcomes (fail / no-fail). | <b>ثنائي</b>: نتيجتان (عطل / بلا عطل). | — |
| <b>Multiclass</b>: one label out of several categories. | <b>متعدد الفئات</b>: تصنيف واحد من عدة فئات. | — |
| <b>Regression</b>: a continuous number, scored on RMSE. | <b>انحدار</b>: قيمة رقمية متصلة، تُقيَّم بـRMSE. | — |
| A clear, specific name. Include the asset or outcome. | اسم واضح ومحدّد. اذكر الأصل أو النتيجة. | — |
| A daily limit stops leaderboard farming; 5/day is a good default. | الحد اليومي يمنع استنزاف لوحة الصدارة؛ وخمس محاولات يوميًا قيمة افتراضية جيدة. | — |
| A reveal date creates suspense and prevents last-minute gaming. | تاريخ الكشف يخلق التشويق ويمنع التلاعب في اللحظات الأخيرة. | — |
| A specific title beats a generic one — name the asset and the outcome. | العنوان المحدّد أفضل من العام — اذكر الأصل والنتيجة. | — |
| Almost there | أوشكت | — |
| Back | السابق | — |
| Choosing the task | اختيار المهمة | — |
| Competition title | عنوان المسابقة | — |
| Conclude it whenever you're ready to close submissions. | أنهِها متى كنت مستعدًا لإغلاق باب المشاركة. | — |
| Create draft | أنشئ مسودة | — |
| Daily submission limit | الحد اليومي للمشاركات | — |
| Describe the business problem, what the target column means, and what a strong solution looks like. This is the first thing competitors read. | صِف المسألة التشغيلية، ومعنى العمود الهدف، وكيف يبدو الحل القوي. هذا أول ما يقرأه المتنافسون. | — |
| ESP Failure Prediction Challenge | تحدي التنبؤ بأعطال مضخات ESP | — |
| Examples: | أمثلة: | — |
| Explain what the target column means in plain language. | اشرح معنى العمود الهدف بلغة بسيطة. | — |
| Fair play & timing | العدالة والتوقيت | — |
| Hit <b>Activate</b> to open submissions — it appears on everyone's leaderboard. | اضغط <b>تفعيل</b> لفتح باب المشاركة — عندها تظهر على لوحة صدارة الجميع. | — |
| Insert a starter template | أدرج قالبًا مبدئيًا | — |
| Keeps the field fair — nobody can brute-force the leaderboard with hundreds of tries. 5 per day suits most challenges. | يحفظ عدالة المنافسة — فلا أحد يستطيع اقتحام لوحة الصدارة بمئات المحاولات. وخمس محاولات يوميًا تناسب معظم التحديات. | — |
| My directorate | إدارتي | — |
| My group | مجموعتي | — |
| My team | فريقي | — |
| Next | التالي | — |
| No reveal date = a simple, always-live leaderboard. | بلا تاريخ كشف = لوحة صدارة بسيطة ومباشرة دائمًا. | — |
| Nothing goes live until you upload data and click Activate. | لا شيء ينطلق حتى ترفع البيانات وتضغط تفعيل. | — |
| On <b>{0}</b> the final private standings are revealed. | في <b>{0}</b> يُكشف الترتيب النهائي الخاص. | — |
| Only people in this scope see and enter the competition. Levels above your access are hidden. | يرى المسابقة ويدخلها من هم ضمن هذا النطاق فقط. والمستويات الأعلى من صلاحيتك مخفية. | — |
| Open the <b>Host</b> tab to upload <b>training</b>, <b>evaluation</b>, and the <b>hidden answer key</b>. | افتح تبويب <b>الاستضافة</b> لرفع بيانات <b>التدريب</b> و<b>التقييم</b> و<b>مفتاح الإجابات المخفي</b>. | — |
| Overview quality | جودة النظرة العامة | — |
| Overview — what competitors predict and why it matters | نظرة عامة — بمَ يتنبأ المتنافسون ولماذا يهم | — |
| Pick the smallest audience that makes sense for the problem. | اختر أضيق جمهور يناسب المسألة. | — |
| Review | المراجعة | — |
| Review reads exactly what competitors will see. | تعرض المراجعة ما سيراه المتنافسون بالضبط. | — |
| State the <b>business value</b>: what decision does a good prediction improve? | اذكر <b>القيمة التشغيلية</b>: أي قرار يُحسّنه التنبؤ الجيد؟ | — |
| The answer key stays Restricted and is never returned by the API. | يبقى مفتاح الإجابات مقيَّدًا ولا تُرجعه الواجهة البرمجية أبدًا. | — |
| The three files you'll prepare | الملفات الثلاثة التي ستجهّزها | — |
| What happens next | ما الذي يحدث بعد ذلك | — |
| What kind of value do competitors predict? This sets the scoring metric and the shape of your data files. | أي نوع من القيم يتنبأ به المتنافسون؟ هذا يحدّد مقياس التقييم وشكل ملفات بياناتك. | — |
| Who can compete | من يمكنه التنافس | — |
| Writing a great brief | كتابة وصف جيد | — |
| You can edit the draft from the Host tab afterwards. | يمكنك تعديل المسودة لاحقًا من تبويب الاستضافة. | — |
| Your <b>id</b> column links each evaluation row to the answer key — keep it unique. | يربط عمود <b>المعرّف</b> كل صف تقييم بمفتاح الإجابات — فاجعله فريدًا. | — |
| Your competition is created as a <b>draft</b> — not yet visible to competitors. | تُنشأ مسابقتك كـ<b>مسودة</b> — غير ظاهرة للمتنافسين بعد. | — |

## CreateDatasetDialog

| English | Arabic | Also on |
|---|---|---|
| Add dataset | أضف مجموعة بيانات | — |
| Description | الوصف | — |
| Name | الاسم | Experiments |
| Security classification | التصنيف الأمني | CreateWorkflowDialog |
| Who can see this? | من يمكنه رؤية هذا؟ | CreateDiscussionDialog |

## CreateDiscussionDialog

| English | Arabic | Also on |
|---|---|---|
| Post discussion | انشر النقاش | — |
| What's on your mind? | بمَ تفكّر؟ | — |

## CreateWorkflowDialog

| English | Arabic | Also on |
|---|---|---|
| Create & open | أنشئ وافتح | — |
| New workflow | مسار عمل جديد | Workflows |
| Personal — explore your own data | شخصي — استكشف بياناتك | — |
| Purpose | الغرض | — |
| Target a competition — submit your pipeline | موجَّه لمسابقة — أرسل مسارك | — |
| Workflow name | اسم مسار العمل | — |
| You'll build a pipeline and submit it — scored on the competition's hidden test set. | ستبني مسارًا وترسله — ويُقيَّم على مجموعة الاختبار المخفية للمسابقة. | — |

## Dashboard

| English | Arabic | Also on |
|---|---|---|
| Active learners | متعلّمون نشطون | — |
| All tracks | كل المسارات | Home, Learn |
| At a glance | لمحة سريعة | — |
| Best rank | أفضل ترتيب | — |
| Best score | أفضل نتيجة | — |
| Competition submissions per person | مشاركات المسابقات لكل شخص | — |
| Competitions entered | مسابقات دخلتها | — |
| Completed | مكتمل | Learn, TrackCertificate |
| Dashboard | لوحة المتابعة | TopNav |
| Enrolled | مسجَّل | Learn |
| Find a competition | ابحث عن مسابقة | — |
| In progress | قيد التقدّم | — |
| learning status | حالة التعلُّم | Home |
| Learning tracks | المسارات التعليمية | Learn |
| Lvl {0} | المستوى {0} | Home, Leaderboards |
| My competition standings | ترتيبي في المسابقات | — |
| My learning | تعلُّمي | — |
| No Barrels earned this month yet. | لم تُكسب براميل هذا الشهر بعد. | — |
| No competition entries yet. | لا مشاركات في المسابقات بعد. | — |
| People | الأفراد | Home, Leaderboards |
| Person | الشخص | — |
| Quiet so far — earn a badge or give kudos to get things moving. | هدوء حتى الآن — اكسب وسامًا أو أرسل تحية لتبدأ الحركة. | — |
| Rank | الترتيب | LiveBoard |
| Refresh | تحديث | Experiments |
| Remaining | متبقٍ | — |
| Start a track or enter a competition to see your progress charted here. | ابدأ مسارًا أو ادخل مسابقة لترى تقدّمك مرسومًا هنا. | — |
| Start one | ابدأ واحدًا | — |
| Submissions made | المشاركات المُرسَلة | — |
| Team at a glance | لمحة عن الفريق | — |
| Team overview — {0} | نظرة على الفريق — {0} | — |
| Team standings (this month) | ترتيب الفرق (هذا الشهر) | — |
| Team standings appear once colleagues start earning Barrels. | يظهر ترتيب الفرق عندما يبدأ الزملاء في كسب البراميل. | Home, Leaderboards |
| Top learners (this month) | الأكثر تعلُّمًا (هذا الشهر) | — |
| Tracks completed | مسارات مكتملة | — |
| Tracks completed per person | المسارات المكتملة لكل شخص | — |
| Viewing as {0} — switch persona from the top-right menu. | تعرض بصفة {0} — بدّل الشخصية من القائمة أعلى اليمين. | — |
| What's happening | ما الذي يجري | — |
| Where you stand across learning and competitions — and, if you lead a team, how your people are doing. | أين تقف في التعلُّم والمسابقات — وإن كنت تقود فريقًا، كيف حال أفراده. | — |
| you | أنت | — |
| You're not enrolled in a track yet. | لم تسجّل في أي مسار بعد. | — |
| Your best score per competition | أفضل نتيجة لك في كل مسابقة | — |
| Your KOC AI journey | رحلتك مع الذكاء الاصطناعي في KOC | — |
| your team | فريقك | Home |
| {0} avg | {0} متوسط | Leaderboards |
| {0} people · {1} bbl total | {0} أشخاص · {1} برميل إجمالًا | — |
| {0}/{1} lessons | {0}/{1} درس | — |

## DatasetVersionsDialog

| English | Arabic | Also on |
|---|---|---|
| Close | إغلاق | InferenceDialog |
| Distinct | قيم مميزة | Datasets |
| Mean | المتوسط | — |
| No versions yet — upload a CSV to create draft v1. | لا إصدارات بعد — ارفع ملف CSV لإنشاء المسودة الأولى. | — |
| Nulls | قيم فارغة | — |
| Publish | نشر | Workflows |
| Type | النوع | Datasets |
| …or import from URL | …أو استورد من رابط | — |

## Datasets

| English | Arabic | Also on |
|---|---|---|
| about {0} rows | نحو {0} صف | — |
| Average | المتوسط | — |
| Build a pipeline | ابنِ مسارًا | — |
| Column details | تفاصيل الأعمدة | — |
| Couldn't open the folder: {0} | تعذّر فتح المجلد: {0} | Models, Settings |
| Couldn't read that file: {0} | تعذّرت قراءة هذا الملف: {0} | — |
| CSV files on this machine. The designer reads them directly, so pipelines run and score without the KOC network. | ملفات CSV على هذا الجهاز. يقرأها المصمّم مباشرة، فتعمل المسارات وتُقيَّم دون شبكة KOC. | — |
| Dataset name | اسم مجموعة البيانات | — |
| Datasets | مجموعات البيانات | DesktopLayout |
| Delete | حذف | Models, Outbox, Runs |
| Delete this dataset? | حذف مجموعة البيانات هذه؟ | — |
| Deleted “{0}”. | حُذف “{0}”. | Models |
| Empty | فارغ | — |
| Highest | الأعلى | — |
| Import a CSV to build your first pipeline. Nothing leaves this machine. | استورد ملف CSV لبناء أول مسار لك. لا شيء يغادر هذا الجهاز. | — |
| Import CSV | استورد ملف CSV | — |
| Import failed: {0} | فشل الاستيراد: {0} | Models |
| Imported {0} file(s). | استُوردت {0} ملفات. | — |
| Lowest | الأدنى | — |
| Nothing to show. | لا شيء لعرضه. | — |
| Open folder | افتح المجلد | Models |
| Reading the file… | جارٍ قراءة الملف… | — |
| Rename | إعادة تسمية | — |
| Rename dataset | إعادة تسمية مجموعة البيانات | — |
| Renamed to “{0}”. | أُعيدت التسمية إلى “{0}”. | — |
| Select a dataset to preview it. | اختر مجموعة بيانات لمعاينتها. | — |
| This file is empty or unreadable. | هذا الملف فارغ أو غير قابل للقراءة. | — |
| Workflows that use this dataset keep working — only the name changes. | مسارات العمل التي تستخدم مجموعة البيانات هذه تظل تعمل — يتغيّر الاسم فقط. | — |
| {0} columns · first {1} rows shown | {0} عمود · تُعرض أول {1} صفوف | — |
| {0} is larger than 512 MB and was skipped. | {0} أكبر من 512 ميغابايت وتم تخطّيه. | — |
| {0} rows, of which {1} were examined | {0} صف، فُحص منها {1} | — |
| “{0}” will be removed from this machine. Workflows that use it will stop resolving. | سيُحذف “{0}” من هذا الجهاز. ومسارات العمل التي تستخدمه لن تعود تجده. | — |

## DemoDisclaimer

| English | Arabic | Also on |
|---|---|---|
| Demonstration environment | بيئة عرض تجريبية | — |
| I understand | أفهم | — |
| You're exploring a demonstration of the {0} platform. The colleagues, competitions, leaderboards, discussions, and datasets shown here are illustrative sample data — not real {1} records — provided to show how the platform works. Nothing here represents actual employees, results, or official company information. | أنت تستعرض نسخةً تجريبيةً من منصة {0}. جميع ما يظهر من زملاء ومسابقات ولوحات صدارة ونقاشات ومجموعات بيانات هو محتوى توضيحي افتراضي — وليس بيانات حقيقية لـ{1} — أُعِدّ لعرض إمكانات المنصة. ولا يمثّل أيٌّ من هذا المحتوى موظفين فعليين أو نتائج أو معلومات رسمية للشركة. | — |

## DesktopLayout

| English | Arabic | Also on |
|---|---|---|
| Checking… | جارٍ التحقق… | — |
| Connected | متصل | — |
| Designer | المصمّم | — |
| Experiments | التجارب | Experiments |
| KOC Studio | استوديو KOC | — |
| Language saved — restart KOC Studio to apply it. | حُفظت اللغة — أعد تشغيل استوديو KOC لتطبيقها. | — |
| Models | النماذج | Models |
| More | المزيد | — |
| Node catalog | دليل العقد | NodeCatalogPage |
| Offline | غير متصل | — |
| Runs | التشغيلات | — |
| Settings | الإعدادات | Settings |
| Train | درِّب | — |
| Workflows | مسارات العمل | Workflows |
| {0} submissions are waiting to be sent. Open to see them. | {0} إرسالات في انتظار الإرسال. افتح لعرضها. | — |
| {0} waiting | {0} في الانتظار | — |

## Error

| English | Arabic | Also on |
|---|---|---|
| Error | خطأ | — |
| Something went wrong | حدث خطأ ما | — |

## Experiments

| English | Arabic | Also on |
|---|---|---|
| Create | إنشاء | — |
| Every training run is tracked here — live trial metrics, the leading model, side-by-side comparison, and the lineage to reproduce it. | تُتابَع هنا كل تشغيلة تدريب — مقاييس المحاولات المباشرة، والنموذج المتصدّر، والمقارنة جنبًا إلى جنب، والسلسلة اللازمة لإعادة الإنتاج. | — |
| Metric | المقياس | — |
| New experiment | تجربة جديدة | — |
| No experiments yet. | لا تجارب بعد. | — |
| No per-trial metrics recorded. | لم تُسجَّل مقاييس لكل محاولة. | — |
| No runs yet. Training into this experiment records runs here. | لا تشغيلات بعد. التدريب ضمن هذه التجربة يسجّل التشغيلات هنا. | — |
| Register this run's model in the registry | سجّل نموذج هذه التشغيلة في السجل | — |
| Run | التشغيلة | — |
| Secondary | مقياس ثانوي | — |
| Select an experiment to see its runs. | اختر تجربة لعرض تشغيلاتها. | — |
| Studio | الاستوديو | — |

## GiveKudosDialog

| English | Arabic | Also on |
|---|---|---|
| Say why (they'll see this) | اذكر السبب (سيرونه) | — |
| Send kudos | أرسل تحية | — |

## Help

| English | Arabic | Also on |
|---|---|---|
| Help | المساعدة | — |
| Help & guides | المساعدة والأدلة | MainLayout |
| Search help | ابحث في المساعدة | — |
| Support | الدعم | — |

## Home

| English | Arabic | Also on |
|---|---|---|
| A company-wide view of KOC's own machine-learning environment — participation across directorates, skills building on company data, and governance keeping it sovereign. | نظرة على مستوى الشركة لبيئة تعلُّم الآلة الخاصة بـKOC — المشاركة عبر الإدارات، والمهارات التي تُبنى على بيانات الشركة، والحوكمة التي تُبقيها سيادية. | — |
| All competitions | كل المسابقات | — |
| All leaderboards | كل لوحات الصدارة | — |
| Barrels | برميل | Profile |
| Browse competitions | تصفّح المسابقات | Leaderboards |
| Browse learning tracks | تصفّح المسارات التعليمية | — |
| Competing | يتنافسون | Leaderboards |
| competitions running | مسابقة جارية | — |
| Competitions running now | مسابقات جارية الآن | — |
| Completed a track | أتمّ مسارًا | — |
| Continue where you left off | تابع من حيث توقّفت | — |
| day streak | يوم متتالٍ | — |
| Enter the arena | ادخل الحلبة | — |
| Featured competition | مسابقة مختارة | — |
| Follow how your team is learning machine learning on KOC data — progress across tracks, competitions entered, and the projects they build. | تابع كيف يتعلّم فريقك تعلُّم الآلة على بيانات KOC — التقدّم في المسارات، والمسابقات التي دخلوها، والمشاريع التي يبنونها. | — |
| Full dashboard | لوحة المتابعة كاملة | — |
| Full leaderboard | لوحة الصدارة كاملة | — |
| Good afternoon | طاب يومك | — |
| Good evening | مساء الخير | — |
| Good morning | صباح الخير | — |
| Guest preview | معاينة كضيف | — |
| KOC's AI & ML workspace. | مساحة KOC للذكاء الاصطناعي وتعلُّم الآلة. | — |
| Learn & grow | تعلَّم وتطوَّر | — |
| Learn machine learning on real KOC subsurface, production, and facilities data — take a track, enter an internal competition, and share your work with colleagues. | تعلَّم تعلُّم الآلة على بيانات KOC الحقيقية في المكامن والإنتاج والمرافق — اسلك مسارًا، وادخل مسابقة داخلية، وشارك عملك مع زملائك. | — |
| Learning | التعلُّم | — |
| Learning now | يتعلّم الآن | — |
| Live standings — {0} | الترتيب المباشر — {0} | — |
| My profile | ملفي | MainLayout |
| Organization pulse | نبض المؤسسة | — |
| See how machine-learning capability is building across your directorate — participation by group, use cases coming into production, and the governance behind them. | اطّلع على نمو قدرات تعلُّم الآلة في إدارتك — المشاركة حسب المجموعة، وحالات الاستخدام التي دخلت الإنتاج، والحوكمة التي تسندها. | — |
| See how machine-learning skills are building across your department on KOC data — who is learning, what teams are working on, and models coming into use. | اطّلع على نمو مهارات تعلُّم الآلة في دائرتك على بيانات KOC — من يتعلّم، وما الذي تعمل عليه الفرق، والنماذج التي دخلت الاستخدام. | — |
| submissions scored | مشاركة مُقيَّمة | — |
| Team standings | ترتيب الفرق | — |
| This month's champions | أبطال هذا الشهر | — |
| Top learners | الأكثر تعلُّمًا | — |
| Top learners this month | الأكثر تعلُّمًا هذا الشهر | — |
| Tracks done | مسارات مكتملة | — |
| View governance | عرض الحوكمة | — |
| View team activity | عرض نشاط الفريق | — |
| Your AI & ML workspace. | مساحتك للذكاء الاصطناعي وتعلُّم الآلة. | — |
| Your department's AI & ML workspace. | مساحة دائرتك للذكاء الاصطناعي وتعلُّم الآلة. | — |
| Your directorate's AI & ML workspace. | مساحة إدارتك للذكاء الاصطناعي وتعلُّم الآلة. | — |
| Your team's AI & ML workspace. | مساحة فريقك للذكاء الاصطناعي وتعلُّم الآلة. | — |
| {0} lessons | {0} دروس | Learn |
| {0} people · {1} avg | {0} أشخاص · {1} متوسط | — |

## InferenceDialog

| English | Arabic | Also on |
|---|---|---|
| Baseline mean | متوسط الأساس | — |
| Batch mean | متوسط الدفعة | — |
| Drift check | فحص الانحراف | — |
| Feature | الخاصية | — |
| Features | الخصائص | — |
| Predict | تنبّأ | Models |
| Prediction | التنبؤ | Models |
| Shift | الإزاحة | — |

## LanguageSwitcher

| English | Arabic | Also on |
|---|---|---|
| Language | اللغة | — |

## Leaderboards

| English | Arabic | Also on |
|---|---|---|
| All time | كل الأوقات | — |
| Concluded | منتهية | — |
| Draft | مسودة | — |
| higher wins | الأعلى يفوز | — |
| Leaderboards | لوحات الصدارة | TopNav |
| leads on {0} | يتصدّر في {0} | — |
| Live | مباشر | — |
| Live now | مباشر الآن | — |
| lower wins | الأدنى يفوز | — |
| No Barrels yet | لا توجد براميل بعد | — |
| No boards yet | لا توجد لوحات ترتيب بعد | — |
| No team standings yet | لا يوجد ترتيب للفرق بعد | — |
| Nobody has earned Barrels in this period. Be the first. | لم يكسب أحد براميل في هذه الفترة. كن الأول. | — |
| Nobody has scored yet | لم يسجّل أحد نتيجة بعد | — |
| Nothing matches “{0}”. | ‏لا نتائج تطابق “{0}”. | — |
| Open competition | فتح المسابقة | — |
| Pick a board to see its standings. | اختر لوحة لعرض ترتيبها. | — |
| Search boards | ابحث في اللوحات | — |
| Sign in to join a competition, download the data and submit a score. | سجّل الدخول للانضمام إلى مسابقة وتنزيل البيانات وإرسال نتيجتك. | — |
| Standings | الترتيب | — |
| Teams | الفرق | — |
| That's you | هذا أنت | — |
| The first competition to open will put its standings here. | أول مسابقة تُفتح ستعرض ترتيبها هنا. | — |
| This board is empty. First submission takes first place. | هذه اللوحة فارغة. أول مشاركة تحتل المركز الأول. | — |
| This month | هذا الشهر | — |
| This week | هذا الأسبوع | — |
| until reveal | حتى الإعلان | — |
| Want your name on this board? | هل تريد اسمك على هذه اللوحة؟ | — |
| You're {0} off rank {1}. | تفصلك {0} عن المركز {1}. | — |
| {0} active · {1} bbl total | {0} نشط · {1} برميل إجمالًا | — |
| {0} boards | {0} لوحة | — |
| {0} competing | {0} متسابقًا | — |
| {0} of {1} boards | {0} من {1} لوحة | — |

## Learn

| English | Arabic | Also on |
|---|---|---|
| All levels | كل المستويات | — |
| Also available in | متاح أيضًا بـ | — |
| best {0}% over {1} attempts | أفضل نتيجة {0}% خلال {1} محاولات | — |
| Certificate | الشهادة | TrackCertificate |
| Content coming soon. | المحتوى قيد الإعداد. | — |
| End-of-track quiz | اختبار نهاية المسار | TrackQuiz |
| Enroll | سجِّل | — |
| Guided tracks from your first look at data to a model your team can rely on. Enroll, work through the lessons, and watch your progress. | مسارات مُرشدة من أول نظرة على البيانات إلى نموذج يعتمد عليه فريقك. سجِّل، وامضِ في الدروس، وتابع تقدّمك. | — |
| Learn | تعلَّم | TopNav |
| Learn & compete | تعلَّم ونافس | — |
| lessons | دروس | TrackCertificate |
| lessons complete | دروس مكتملة | — |
| Mark complete | علِّمه مكتملًا | — |
| No track matches these filters. Widen them and something will turn up. | لا يوجد مسار يطابق هذه المرشّحات. وسّعها وستظهر نتائج. | — |
| One step left — pass the quiz to finish. | بقيت خطوة واحدة — اجتز الاختبار لإنهاء المسار. | — |
| Only in {0} | متاح بـ{0} فقط | — |
| Open | افتح | Workflows |
| Optional self-check | اختبار ذاتي اختياري | TrackQuiz |
| Passed | ناجح | TrackQuiz |
| Put it to work in | طبِّقه في | — |
| Required to finish this track | مطلوب لإنهاء هذا المسار | TrackQuiz |
| Review or retake | راجع أو أعد المحاولة | — |
| Search tracks | ابحث في المسارات | — |
| Sign in to take the quiz. | سجّل الدخول لأداء الاختبار. | — |
| Take the quiz | ابدأ الاختبار | — |
| The learning catalogue couldn't be loaded ({0}). | تعذّر تحميل دليل المسارات ({0}). | — |
| tracks | مسارات | — |
| Try again | حاول مرة أخرى | — |
| {0} / {1} complete | أنجزت {0} من {1} | — |
| {0} of {1} tracks | {0} من {1} مسار | — |
| {0} questions · {1}% to pass | {0} سؤالًا · {1}% للنجاح | — |
| {0} tracks | {0} مسار | — |

## LiveBoard

| English | Arabic | Also on |
|---|---|---|
| Competitor | المتنافس | — |
| No submissions yet — be the first on the board! | لا مشاركات بعد — كن الأول على اللوحة! | — |

## Login

| English | Arabic | Also on |
|---|---|---|
| Create the first one | أنشئ الحساب الأول | — |
| it becomes the platform administrator. | وسيصبح مشرف المنصة. | — |
| No account yet? | لا تملك حسابًا بعد؟ | — |
| No accounts exist yet. | لا توجد حسابات بعد. | — |
| Password | كلمة المرور | Register |
| Register | سجِّل | — |
| Sign in to compete, build pipelines, and track your standing. | سجّل الدخول للتنافس، وبناء المسارات، ومتابعة ترتيبك. | — |
| Work email | بريد العمل | Register |

## MainLayout

| English | Arabic | Also on |
|---|---|---|
| Admin | الإدارة | — |
| Back to home | العودة إلى الرئيسية | — |
| Console | لوحة التحكم | — |
| Sign in to continue | سجّل الدخول للمتابعة | — |
| Sign in with KOC | تسجيل الدخول بحساب KOC | — |
| Sign out | تسجيل الخروج | — |
| This area is for signed-in members. | هذه المنطقة للأعضاء المسجَّلين. | — |
| View as Employee | عرض كموظف | — |
| View the app as (dev) | عرض التطبيق بصفة (تطوير) | — |
| You don't have access to this area | لا تملك صلاحية الوصول إلى هذه المنطقة | — |
| Your role ({0}) can't view this page. Supervision needs a team-lead role or above; Admin needs Platform Admin. | دورك ({0}) لا يسمح بعرض هذه الصفحة. الإشراف يتطلب دور قائد فريق فما فوق؛ والإدارة تتطلب مشرف المنصة. | — |

## Models

| English | Arabic | Also on |
|---|---|---|
| Built with | بُني بـ | — |
| Columns it needs | الأعمدة التي يحتاجها | — |
| Confidence {0} | الثقة {0} | — |
| Delete this model? | حذف هذا النموذج؟ | — |
| Details | التفاصيل | — |
| Export | تصدير | Workflows |
| Export failed: {0} | فشل التصدير: {0} | — |
| Exported to {0} | صُدِّر إلى {0} | — |
| Import a model | استورد نموذجًا | — |
| Import this model? | استيراد هذا النموذج؟ | — |
| Imported “{0}” as v{1}. | استُورد “{0}” بوصفه الإصدار {1}. | — |
| ML.NET {0} | ML.NET {0} | — |
| Models kept on this machine. These are local drafts — they have not been through the platform's review, and nothing here is deployed. | نماذج محفوظة على هذا الجهاز. هذه مسودات محلية — لم تمرّ بمراجعة المنصّة، ولا شيء منها منشور للتشغيل. | — |
| No models kept yet | لا نماذج محفوظة بعد | — |
| Pick a CSV with the same columns this model was trained on. The predictions are written to a new file beside it. | اختر ملف CSV بالأعمدة نفسها التي دُرِّب عليها هذا النموذج. تُكتب التنبؤات في ملف جديد بجواره. | — |
| Predicting | التنبؤ بـ | Runs |
| Score a file | قيّم ملفًا | — |
| Score the file | قيّم الملف | — |
| Select a model to use it. | اختر نموذجًا لاستخدامه. | — |
| Sending a model to the platform is not available yet — the platform registers models from runs made on the server. Export it and attach it to a request for now. | إرسال نموذج إلى المنصّة غير متاح بعد — فالمنصّة تسجّل النماذج من تشغيلات تجري على الخادم. صدّر النموذج وأرفقه بطلب في الوقت الحالي. | — |
| Size | الحجم | — |
| That didn't work: {0} | لم ينجح ذلك: {0} | Outbox, Runs |
| That file could not be imported. | تعذّر استيراد هذا الملف. | — |
| That file had no rows to score. | لا صفوف في هذا الملف لتقييمها. | — |
| That file is missing columns this model needs: {0} | ينقص هذا الملف أعمدة يحتاجها النموذج: {0} | — |
| The model's file is missing. | ملف النموذج مفقود. | — |
| Train a model, then choose “Keep this model” to save it here. | درِّب نموذجًا ثم اختر «احتفظ بهذا النموذج» لحفظه هنا. | — |
| Trained on {0} | دُرِّب على {0} | — |
| Try one row | جرّب صفًا واحدًا | — |
| v{0} | إصدار {0} | — |
| {0} MB | {0} ميجابايت | — |
| {0} MB on disk | {0} ميجابايت على القرص | Runs |
| {0} rows scored. Saved as {1} | تمّ تقييم {0} صفًا. حُفظت باسم {1} | — |
| “{0}” v{1} will be removed from this machine. | سيُحذف “{0}” الإصدار {1} من هذا الجهاز. | — |
| “{0}” will run on this machine when you use it. Only import models from people you trust. | سيعمل “{0}” على هذا الجهاز عند استخدامه. لا تستورد النماذج إلا ممّن تثق بهم. | — |

## NodeCatalogPage

| English | Arabic | Also on |
|---|---|---|
| ML node catalog | دليل عقد تعلُّم الآلة | — |

## NodePropertyPanel

| English | Arabic | Also on |
|---|---|---|
| No datasets with a file are visible to you — add one under Datasets to join or append it. | لا توجد مجموعات بيانات بملفات ظاهرة لك — أضف واحدة من مجموعات البيانات لدمجها أو إلحاقها. | — |
| No options — this step acts on the columns flowing into it. | لا خيارات — تعمل هذه الخطوة على الأعمدة الواردة إليها. | — |

## NotificationBell

| English | Arabic | Also on |
|---|---|---|
| Mark all read | تعليم الكل كمقروء | — |
| Notifications | الإشعارات | — |
| You're all caught up. | لا جديد لديك. | — |

## Outbox

| English | Arabic | Also on |
|---|---|---|
| Delete this submission? | حذف هذا الإرسال؟ | — |
| Everything you have submitted has been sent. | أُرسل كل ما قدّمته. | — |
| It will not be sent, and the predictions kept with it are removed from this machine. | لن يُرسَل، وستُحذف التنبؤات المحفوظة معه من هذا الجهاز. | — |
| Needs attention | يحتاج إلى مراجعة | — |
| Not scored yet — the platform scores it against the hidden test set when it arrives. | لم تُقيَّم بعد — تقيّمها المنصّة على مجموعة الاختبار المخفية عند وصولها. | — |
| Nothing could be sent this time. | تعذّر إرسال أي شيء هذه المرة. | — |
| Nothing waiting | لا شيء في الانتظار | — |
| Queued | في الانتظار | — |
| Queued {0} | في الانتظار منذ {0} | — |
| Refused | مرفوض | — |
| Sent {0}, and {1} were refused. | أُرسل {0}، ورُفض {1}. | — |
| Sent {0}. | أُرسل {0}. | — |
| Still no connection to the KOC network. | لا يزال لا اتصال بشبكة شركة نفط الكويت. | — |
| Submissions made without a network wait here and are sent when it returns. Sending again is safe — the platform records each one only once. | الإرسالات التي تُنفَّذ دون شبكة تنتظر هنا وتُرسل عند عودتها. إعادة الإرسال آمنة — تُسجّل المنصّة كل إرسال مرة واحدة فقط. | — |
| Try now | جرّب الآن | — |
| Waiting to send | في انتظار الإرسال | — |
| your local score {0} | نتيجتك المحلية {0} | — |
| {0} were refused — see why below. | رُفض {0} — انظر السبب أدناه. | — |

## PodiumBlock

| English | Arabic | Also on |
|---|---|---|
| The podium is wide open — be the first on the board! | المنصة مفتوحة للجميع — كن الأول على اللوحة! | — |

## Profile

| English | Arabic | Also on |
|---|---|---|
| About | نبذة | — |
| About you | نبذة عنك | — |
| Achievements | الإنجازات | — |
| Badge wall | جدار الأوسمة | — |
| Earn badges by learning, competing, and helping colleagues. Locked badges show what's next. | اكسب الأوسمة بالتعلُّم والتنافس ومساعدة الزملاء. والأوسمة المقفلة تُريك ما هو قادم. | — |
| Give kudos | أرسل تحية | — |
| Kudos wall | جدار التحيات | — |
| No kudos yet. Great work gets noticed — keep going! | لا تحيات بعد. العمل الجيد يُلاحَظ — واصل! | — |
| Pick an avatar | اختر صورة رمزية | — |
| Profile | الملف الشخصي | — |
| Save | حفظ | Settings |
| Skills | المهارات | — |
| Skills (comma-separated) | المهارات (مفصولة بفواصل) | — |

## Register

| English | Arabic | Also on |
|---|---|---|
| Already registered? | مسجَّل بالفعل؟ | — |
| At least 8 characters, with an upper and lower case letter and a digit. | ثمانية أحرف على الأقل، مع حرف كبير وآخر صغير ورقم. | — |
| Create account | إنشاء الحساب | — |
| Create an account | إنشاء حساب | — |
| Create your account | أنشئ حسابك | — |
| Join the community, enter competitions, and build pipelines. | انضم إلى المجتمع، وادخل المسابقات، وابنِ المسارات. | — |
| Shown on leaderboards | يظهر على لوحات الصدارة | — |
| This is the first account on this installation, so it becomes the platform administrator. | هذا أول حساب في هذا التنصيب، ولذلك سيصبح مشرف المنصة. | — |
| Your name | اسمك | — |

## RegisterModelDialog

| English | Arabic | Also on |
|---|---|---|
| From training run | من تشغيلة تدريب | — |
| Model name | اسم النموذج | Runs |
| Register version | سجّل الإصدار | — |

## RunWorkflowDialog

| English | Arabic | Also on |
|---|---|---|
| Experiment | التجربة | — |

## Runs

| English | Arabic | Also on |
|---|---|---|
| Attempt log | سجل المحاولات | — |
| Attempts | المحاولات | — |
| Compare | قارن | — |
| Comparing {0} runs | مقارنة {0} تشغيلات | — |
| Data fingerprint | بصمة البيانات | — |
| Delete this run? | حذف هذا التشغيل؟ | — |
| Failed | فشل | — |
| Hit the limit | بلغ الحدّ | — |
| Its metrics and saved model are removed from this machine. | ستُحذف مقاييسه ونموذجه المحفوظ من هذا الجهاز. | — |
| Keep | احتفظ | — |
| Keep this model | احتفظ بهذا النموذج | — |
| Kept as “{0}” v{1}. | حُفظ باسم “{0}” الإصدار {1}. | — |
| Kept on this machine, with the settings each run used. Delete a run to reclaim its disk space. | محفوظة على هذا الجهاز مع الإعدادات التي استخدمها كل تشغيل. احذف تشغيلًا لاستعادة مساحته على القرص. | — |
| Limits used | الحدود المستخدمة | — |
| Model saved | حُفظ النموذج | — |
| No | لا | — |
| No runs yet | لا تشغيلات بعد | — |
| Reuse a name to save this as the next version of it. | أعد استخدام اسم لحفظ هذا بوصفه إصداره التالي. | — |
| Rows | الصفوف | — |
| Run deleted. | حُذف التشغيل. | — |
| Select a run to see its details. | اختر تشغيلًا لعرض تفاصيله. | — |
| Stopped | متوقّف | — |
| The dataset has changed since this run. Training it again will not give the same result. | تغيّرت مجموعة البيانات منذ هذا التشغيل. إعادة التدريب لن تعطي النتيجة نفسها. | — |
| This run's model file is missing. | ملف نموذج هذا التشغيل مفقود. | — |
| Train a model and it will be recorded here. | درِّب نموذجًا وسيُسجَّل هنا. | — |
| Yes | نعم | — |
| {0}s | {0} ث | — |
| {0}s · {1} MB | {0} ث · {1} ميجابايت | — |

## Settings

| English | Arabic | Also on |
|---|---|---|
| A run stops when it reaches either limit. Raise them for a bigger dataset; lower them if your machine struggles while training. | يتوقّف التشغيل عند بلوغ أيٍّ من الحدّين. ارفعهما لمجموعة بيانات أكبر، وأخفضهما إذا تعثّر جهازك أثناء التدريب. | — |
| Act as (dev override) | التصرّف بصفة (تجاوز للتطوير) | — |
| Between 10 seconds and 1 hour. | بين 10 ثوانٍ وساعة واحدة. | — |
| Between 512 MB and 16 GB. This machine has {0} MB in use right now. | بين 512 ميجابايت و16 جيجابايت. يستخدم هذا الجهاز {0} ميجابايت حاليًا. | — |
| company — | الشركة — | — |
| Connected to the platform. Applied on next launch. | متصل بالمنصّة. يُطبَّق عند التشغيل التالي. | — |
| Connection to the KOC platform database. Leave empty to work offline — datasets, the designer, training and models all still work. | الاتصال بقاعدة بيانات منصّة شركة نفط الكويت. اتركه فارغًا للعمل دون اتصال — تظل مجموعات البيانات والمصمّم والتدريب والنماذج تعمل جميعها. | — |
| Department and profile details load from the KOC directory once its API is configured. | تُحمَّل تفاصيل الدائرة والملف الشخصي من دليل شركة نفط الكويت بمجرد إعداد واجهته البرمجية. | — |
| department — | الدائرة — | — |
| Detailed logging | تسجيل مفصّل | — |
| Diagnostics | التشخيص | — |
| email — | البريد الإلكتروني — | — |
| Identity | الهوية | — |
| KOC network | شبكة شركة نفط الكويت | — |
| KOC Studio {0} · WebView2 {1} | استوديو KOC {0} · WebView2 {1} | — |
| Last workspace check | آخر فحص لمساحة العمل | — |
| Leave as the signed-in user in production. The dev personas are for testing roles against a dev API. | اتركه على المستخدم المسجَّل في بيئة الإنتاج. شخصيات التطوير مخصّصة لاختبار الأدوار مقابل واجهة برمجة تطويرية. | — |
| Limits saved — they apply to the next run. | حُفظت الحدود — تُطبَّق على التشغيل التالي. | — |
| Local only — competitions and experiments are unavailable. | محلي فقط — المسابقات والتجارب غير متاحة. | — |
| Memory ceiling (MB) | سقف الذاكرة (ميجابايت) | — |
| not detected | غير مكتشف | — |
| Open logs | افتح السجلات | — |
| Open workspace | افتح مساحة العمل | — |
| Platform database | قاعدة بيانات المنصّة | — |
| Records far more detail for a support session. Applies on the next launch. | يسجّل تفاصيل أكثر بكثير لجلسة دعم. يُطبَّق عند التشغيل التالي. | — |
| Run history is using {0} MB. | يستهلك سجل التشغيلات {0} ميجابايت. | — |
| Saved. Restart to apply a changed database or address. | تمّ الحفظ. أعد التشغيل لتطبيق تغيير قاعدة البيانات أو العنوان. | — |
| Signed in as | مسجَّل الدخول باسم | — |
| Signed in from your intranet session — no extra login. | مسجَّل الدخول من جلسة الشبكة الداخلية — بلا تسجيل دخول إضافي. | — |
| Signed-in user ({0}) | المستخدم المسجَّل ({0}) | — |
| Time budget (seconds) | المدة المسموحة (بالثواني) | — |
| Training limits | حدود التدريب | — |
| Used for the live leaderboard only — everything else is read from the database below. Applied on next launch. | يُستخدم للوحة الصدارة الحيّة فقط — وكل ما عداها يُقرأ من قاعدة البيانات أدناه. يُطبَّق عند التشغيل التالي. | — |
| Website address | عنوان الموقع | — |
| Workspace checked at launch — nothing to report. | فُحصت مساحة العمل عند التشغيل — لا شيء يستدعي الإبلاغ. | — |

## Setup

| English | Arabic | Also on |
|---|---|---|
| Restart the app to apply it. | أعد تشغيل التطبيق لتطبيقها. | — |
| Save and continue | احفظ وتابع | — |
| Setup saved | حُفظت الإعدادات | — |

## SortKeyBuilder

| English | Arabic | Also on |
|---|---|---|
| Order | الترتيب | — |

## SqlConditionBuilder

| English | Arabic | Also on |
|---|---|---|
| Operator | المُعامل | — |
| Value | القيمة | — |

## StandingChip

| English | Arabic | Also on |
|---|---|---|
| Level {0} · {1} | المستوى {0} · {1} | — |

## Supervision

| English | Arabic | Also on |
|---|---|---|
| Supervision | الإشراف | TopNav |

## TopNav

| English | Arabic | Also on |
|---|---|---|
| Home | الرئيسية | — |
| Main | الرئيسية | — |

## TrackCertificate

| English | Arabic | Also on |
|---|---|---|
| A certificate appears here once you finish the track. | تظهر الشهادة هنا بعد إنهاء المسار. | — |
| Back to the track | العودة إلى المسار | TrackQuiz |
| Certificate of completion | شهادة إتمام | — |
| has completed the learning track | قد أتمّ المسار التعليمي | — |
| Level | المستوى | — |
| Nothing to show yet | لا شيء لعرضه بعد | — |
| Print | طباعة | — |
| Quiz | اختبار | TrackQuiz |
| Reference {0} | المرجع {0} | — |
| This is to certify that | تشهد هذه الوثيقة بأن | — |

## TrackQuiz

| English | Arabic | Also on |
|---|---|---|
| Answer every question before submitting — {0} left. | أجب عن كل الأسئلة قبل الإرسال — بقي {0}. | — |
| answered | مُجاب عنها | — |
| Marking… | جارٍ التصحيح… | — |
| No questions yet | لا توجد أسئلة بعد | — |
| Not this time | ليس هذه المرة | — |
| Retake the quiz | أعد الاختبار | — |
| Submit answers | أرسل الإجابات | — |
| That finished the track. Your Barrels and badge are on your profile. | بذلك أنهيت المسار. براميلك وشارتك في ملفك الشخصي. | — |
| This quiz has not been written yet. Nothing to do here for now. | لم تُكتب أسئلة هذا الاختبار بعد. لا شيء لفعله هنا حاليًا. | — |
| This quiz is required to finish the track. You can retake it as often as you like. | هذا الاختبار مطلوب لإنهاء المسار، ويمكنك إعادته كما تشاء. | — |
| your answer | إجابتك | — |
| {0} of {1} correct — {2}% | {0} من {1} صحيحة — {2}% | — |
| {0}% needed | المطلوب {0}% | — |

## Workflows

| English | Arabic | Also on |
|---|---|---|
| Archive | أرشفة | — |
| Create one, or instantiate a template on the right. | أنشئ واحدًا، أو استخدم قالبًا من اليمين. | — |
| No workflows yet | لا مسارات عمل بعد | — |
| Studio · registry | الاستوديو · السجل | — |
| Templates | القوالب | — |
| Use this template | استخدم هذا القالب | — |
| Version your ML pipelines: save drafts, publish an immutable snapshot once it compiles, then export or roll a new draft. Start from an O&G template. | أصدِر نسخًا من مسارات تعلُّم الآلة: احفظ المسودات، وانشر لقطة ثابتة بمجرد نجاح البناء، ثم صدّرها أو ابدأ مسودة جديدة. وابدأ من قالب للنفط والغاز. | — |

## XpBoard

| English | Arabic | Also on |
|---|---|---|
| Earn the first Barrels of the month and this board is yours. | اكسب أول براميل الشهر وتكون هذه اللوحة لك. | — |

## Shared / set in code

| English | Arabic | Also on |
|---|---|---|
| Active | جارية | — |
| AI Learning Community | مجتمع تعلّم الذكاء الاصطناعي | — |
| Build AI skills, compete with colleagues, and grow your career at KOC. | طوّر مهاراتك في الذكاء الاصطناعي، ونافس زملاءك، وامضِ قدمًا في مسارك المهني في شركة نفط الكويت. | — |
| Company | الشركة | — |
| Directorate | الإدارة | — |
| Group | المجموعة | — |
| KOC T&CD | التدريب والتطوير الوظيفي | — |
| KOC Training and Career Development | منصة مجموعة التدريب والتطوير الوظيفي - شركة نفط الكويت | — |
| Kuwait Oil Company | شركة نفط الكويت | — |
| Sign in to {0} | سجّل الدخول لـ{0} | — |
| Sign in with KOC to {0} | سجّل الدخول بحساب KOC لـ{0} | — |
| Sign in with your account, or register if you don't have one yet. | سجّل الدخول بحسابك، أو أنشئ حسابًا إن لم يكن لديك بعد. | — |
| Team | الفريق | — |
| Use the persona picker in the top right to view the app as different roles. | استخدم منتقي الشخصيات أعلى اليمين لعرض التطبيق بأدوار مختلفة. | — |
| You are signed in automatically with your KOC account — if you are seeing this, contact the platform team. | يتم تسجيل دخولك تلقائيًا بحساب KOC — إن ظهرت لك هذه الرسالة فتواصل مع فريق المنصة. | — |
| {0} — lower wins | {0} — الأقل يفوز | — |

