using System.Globalization;
using System.Text;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Competitions;

/// <summary>
/// The competitions the platform ships with, in every environment: a curated company-wide set spanning
/// KOC's operational domains and Ahmadi Hospital, each complete with a participant-visible training set,
/// an evaluation feature set, and the hidden answer key — so a Studio pipeline can be submitted end to
/// end and land on the leaderboard on the day a site goes live.
/// <para>
/// Between them they cover every prediction type the platform can score: binary and multiclass
/// classification (accuracy), regression and time-series forecasting (RMSE), and unsupervised anomaly
/// detection (AUC).
/// </para>
/// <para>
/// One dataset is real — the Titanic manifest, vendored and embedded (see <c>Data/README.md</c>). Every
/// other is <b>generated deterministically</b> from a stated relationship plus noise: same seed, same
/// data, every install, so the signal is genuinely learnable and no one mistakes a challenge for
/// operational or patient records.
/// </para>
/// <para>
/// None of them are clean, and that is deliberate. <see cref="Mess"/> puts back what a tutorial dataset
/// takes out — gaps where a reading was never taken, <c>-999</c> where an instrument failed, and columns
/// logged in two units with the unit recorded beside them — because cleaning is most of the work in
/// practice and none of it in a worked example. The two unsupervised challenges are the exception:
/// injected junk would be the most abnormal thing in the file, so finding it would score as success.
/// </para>
/// <para>
/// Idempotent per-title: a competition an administrator has edited or concluded is left alone, and a
/// title added in a later release appears without a database reset. People and their submissions are
/// not seeded here — those arrive from real members, or from the demo data an administrator asks for.
/// </para>
/// </summary>
public static class CompetitionSeeder
{
    /// <summary>
    /// Who the seeded competitions belong to. Not a person: these ship with the product, and naming a
    /// dev persona (as an earlier release did) points at an account that does not exist on a real
    /// deployment.
    /// </summary>
    private const string Owner = "koc-platform";

    private const string TitanicTitle = "Titanic — Who Survives? (Starter Challenge)";
    private const string WellIntegrityTitle = "Well Integrity — Does This Well Need Intervention?";
    private const string EspTitle = "ESP Pump Failure — Predictive Maintenance";
    private const string ProductionTitle = "Daily Oil Rate — Predict the Flow";
    private const string ProductionForecastTitle = "Production Decline — Forecast the Next 90 Days";
    private const string AnomalyTitle = "ESP Fault Watch — Flag Abnormal Sensor Rows";
    private const string FaciesTitle = "Rock Facies from Well Logs";
    private const string PorosityTitle = "Log Porosity — Predict the Pore Space";
    private const string RopTitle = "Rate of Penetration — How Fast Will We Drill?";
    private const string CorrosionTitle = "Pipeline Corrosion Risk — Low, Medium or High";
    private const string NearMissTitle = "Near-Miss Severity — How Bad Could It Have Been?";
    private const string DiabetesTitle = "Diabetes Screening from a Routine Lab Panel";
    private const string LabQcTitle = "Lab Analyser QC — Catch the Drifting Run";
    private const string TriageTitle = "Emergency Triage — Assign the Acuity Level";
    private const string StayTitle = "Length of Stay — How Many Days in Hospital?";
    private const string CourseTitle = "Course Completion — Who Will Not Finish?";
    private const string DemandTitle = "Course Demand — How Many Seats Next Quarter?";
    private const string CompetencyTitle = "Time to Competency — How Long Until Sign-Off?";
    private const string NextTrackTitle = "What Should They Learn Next?";
    private const string RatingTitle = "Course Rating — Will This Session Disappoint?";
    private const string PayrollTitle = "Payroll Integrity — Catch the Bad Run";
    private const string AbsenceTitle = "Absence Planning — How Many Days Will the Site Lose?";
    private const string HireTitle = "Time to Hire — How Long Until the Post Is Filled?";
    private const string OvertimeTitle = "Overtime Cap — Which Teams Will Go Over?";
    private const string GradeTitle = "Job Evaluation — Which Grade Band Is This Role?";

    /// <summary>The line every generated dataset carries, so its nature is never in doubt.</summary>
    private const string Synthetic = " The dataset is synthetic — generated from a stated relationship plus noise, "
        + "not taken from operational records.";

    /// <summary>The same line for the hospital challenges, where being unambiguous matters more.</summary>
    private const string SyntheticClinical = " The dataset is synthetic: it is generated to behave like the "
        + "hospital's routine panels, and contains no patient records of any kind.";

    /// <summary>
    /// Said on every challenge whose extract has been dirtied. Cleaning is most of the job in practice
    /// and none of it in a tutorial, so the datasets here arrive in the state real ones do.
    /// </summary>
    private const string Dirty = " The extract is not clean. Readings go missing, and a failed instrument "
        + "writes -999 rather than nothing — which is worse, because it is a number, and it will drag your "
        + "averages and your scaling with it. Replace missing and Filter rows exist for this.";

    public static async Task SeedCompetitionsAsync(KocDbContext db, IArtifactService artifacts, CancellationToken ct = default)
    {
        var stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Learn ↔ compete tie-ins.
        var starterTrack = await TrackId(db, "AI for Everyone — Start Here", ct)
            ?? await TrackId(db, "Getting started with data", ct);
        var solveTrack = await TrackId(db, "Solve a real problem", ct);

        // ---- Start here -------------------------------------------------------------------------

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            TitanicTitle,
            "The world's most famous first machine-learning problem — perfect for a first try, no oil-&-gas "
            + "knowledge needed. From a passenger's details (class, sex, age, fare, family aboard), predict "
            + "who survived. Build a pipeline in the Studio, submit it, and see your accuracy on a hidden test "
            + "set. A great way to *see the idea* behind the whole platform. "
            + "This is the **real manifest** — 891 actual passengers of the RMS Titanic, not a simulation — "
            + "so it arrives with the gaps history left in it: 177 ages were never recorded, and two "
            + "passengers have no port of embarkation. Decide what to do about that before you fit; "
            + "throwing away every row with a missing age costs you a fifth of the ship.",
            "accuracy", "survived", "BinaryClassification", BuildTitanic(),
            // Deliberately uncategorised: it belongs to no KOC domain, and that is the point of it.
            null, starterTrack, IsFeatured: true,
            TitleAr: "تايتانيك — مَن نجا؟ (تحدي البداية)",
            DescriptionAr: "أشهر مسألة أولى في تعلّم الآلة، ولا تتطلّب أي معرفة بالنفط والغاز. من بيانات الراكب — الدرجة والجنس والعمر وقيمة التذكرة ورفقة العائلة — توقّع مَن نجا. البيانات هي **قائمة الركّاب الحقيقية**: ٨٩١ راكباً فعلياً، بما فيها الثغرات التي تركها التاريخ — ١٧٧ عمراً لم يُسجَّل قط. قرّر كيف تتعامل معها قبل التدريب؛ فحذف كل صف ناقص العمر يكلّفك خُمس السفينة. التقييم بالدقّة."));

        // ---- Subsurface -------------------------------------------------------------------------

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            FaciesTitle,
            "Name the rock. From five well-log measurements — gamma ray, resistivity, bulk density, neutron "
            + "porosity and photoelectric factor — classify each interval as sandstone, shale, limestone or "
            + "dolomite. A geologist does this by eye all day; here you teach a model to do it. Four classes, "
            + "scored on accuracy. The photoelectric log is the one that tells limestone from dolomite — and "
            + "it is also the one that is missing on the older wells, which is the whole problem in one "
            + "column." + Synthetic + Dirty,
            "accuracy", "facies", "MulticlassClassification",
            Dirtied(BuildFacies(), 20260205, "facies", new Mess(
                Gaps: ["photoelectric", "neutron_porosity"],
                Sentinels: ["resistivity"])),
            "subsurface", solveTrack,
            LegacyTitle: "Rock Facies from Well Logs (Demo)",
            TitleAr: "سحنات الصخور من سجلات الآبار",
            DescriptionAr: "سَمِّ الصخر. من خمس قياسات لسجلات الآبار — أشعة غاما، والمقاومية، والكثافة الكتلية، والمسامية النيوترونية، والعامل الكهروضوئي — صنّف كل فترة إلى حجر رملي أو صخر طيني أو حجر جيري أو دولوميت. السجل الكهروضوئي هو ما يميّز الجيري عن الدولوميت، وهو نفسه المفقود في الآبار الأقدم — وتلك هي المسألة كلها في عمود واحد. أربعة أصناف، والتقييم بالدقّة. البيانات مُولّدة، وغير نظيفة عمداً."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            PorosityTitle,
            "How much pore space is in the rock? Predict porosity (%) from the same log suite — gamma ray, "
            + "resistivity, bulk density, neutron porosity and photoelectric factor. The physics is on your "
            + "side: bulk density falls as porosity rises. The catch is shale, which pushes the neutron log up "
            + "and will mislead a model that ignores gamma ray. Regression, scored on RMSE (lower is better)."
            + Synthetic + Dirty,
            "rmse", "porosity", "Regression",
            Dirtied(BuildPorosity(), 20260206, "porosity", new Mess(
                Gaps: ["neutron_porosity", "photoelectric"],
                Sentinels: ["density"])),
            "subsurface", solveTrack,
            TitleAr: "المسامية من السجلات — قدّر حجم المسام",
            DescriptionAr: "كم من الفراغ في الصخر؟ توقّع المسامية (٪) من طقم السجلات نفسه. الفيزياء في صالحك: الكثافة الكتلية تنخفض كلما ارتفعت المسامية. والمِصْيَدة هي الصخر الطيني، الذي يرفع قراءة السجل النيوتروني ويضلّل أي نموذج يتجاهل أشعة غاما. انحدار، والتقييم بجذر متوسط مربّع الخطأ (الأقل أفضل)."));

        // ---- Drilling & Wells -------------------------------------------------------------------

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            WellIntegrityTitle,
            "Decide which wells to send a rig to. From routine surveillance readings — casing and annulus "
            + "pressure, temperature, water cut and well age — predict whether a well needs intervention. "
            + "Sustained annulus pressure is the signal everyone watches; the interesting question is what it "
            + "misses on its own. Binary classification, scored on accuracy. Mind the pressures: two field "
            + "teams recorded them, one in psi and one in bar, and the `casing_pressure_unit` column is the "
            + "only thing telling you which. Put them on one scale with a Compute column node before you "
            + "fit, or the model learns two overlapping populations and splits the difference."
            + Synthetic + Dirty,
            "accuracy", "label", "BinaryClassification",
            Dirtied(BuildWellIntegrity(), 20260207, "label", new Mess(
                Gaps: ["temperature", "water_cut"],
                Sentinels: ["annulus_pressure"],
                Units: new UnitSwitch("casing_pressure", "casing_pressure_unit", "psi", "bar", psi => psi / 14.5038, 0.35))),
            "drilling-wells", solveTrack,
            LegacyTitle: "Well Integrity — Intervention Needed? (Demo)",
            TitleAr: "سلامة الآبار — هل يحتاج هذا البئر إلى تدخّل؟",
            DescriptionAr: "قرّر إلى أي الآبار تُرسل الحفّارة. من قراءات المراقبة الدورية — ضغط التغليف والحيّز الحلقي، والحرارة، ونسبة الماء، وعمر البئر — توقّع ما إذا كان البئر يحتاج تدخّلاً. انتبه للضغوط: سجّلها فريقان، أحدهما بوحدة psi والآخر بالبار، وعمود الوحدة وحده يخبرك أيّهما. وحّدها قبل التدريب. تصنيف ثنائي، والتقييم بالدقّة."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            RopTitle,
            "Predict rate of penetration (m/hr) from the driller's own controls — weight on bit, rotary speed "
            + "and mud flow rate — against what the hole gives you: formation hardness, bit wear and depth. "
            + "Every metre per hour is rig time, so the model that reads the trade-off best wins. Regression, "
            + "scored on RMSE (lower is better). Depth comes off two different systems — metres on some "
            + "runs, feet on others, with `depth_m_unit` saying which. Convert before you fit."
            + Synthetic + Dirty,
            "rmse", "rop", "Regression",
            Dirtied(BuildRop(), 20260208, "rop", new Mess(
                Gaps: ["mud_flow_rate", "bit_wear"],
                Sentinels: ["rotary_rpm"],
                Units: new UnitSwitch("depth_m", "depth_m_unit", "m", "ft", m => m * 3.28084, 0.3))),
            "drilling-wells", solveTrack,
            TitleAr: "معدّل الاختراق — ما سرعة الحفر؟",
            DescriptionAr: "توقّع معدّل الاختراق (م/ساعة) من أدوات الحفّار نفسه — الوزن على المِثقب، وسرعة الدوران، ومعدّل تدفّق الطين — في مقابل ما تفرضه الطبقة: صلابة التكوين، وتآكل المِثقب، والعمق. كل متر في الساعة هو وقت حفّارة. والعمق مسجّل بالأمتار أحياناً وبالأقدام أحياناً أخرى. انحدار، والتقييم بجذر متوسط مربّع الخطأ."));

        // ---- Production -------------------------------------------------------------------------

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            ProductionTitle,
            "Predict a well's daily oil rate (bopd) from choke size, tubing-head pressure, water cut, gas rate "
            + "and days online. The workhorse regression problem of any production office — and a good place "
            + "to find out how much your feature engineering is really worth. Scored on RMSE (lower is "
            + "better)." + Synthetic + Dirty,
            "rmse", "oil_rate", "Regression",
            Dirtied(BuildProduction(), 20260209, "oil_rate", new Mess(
                Gaps: ["gas_rate", "water_cut"],
                Sentinels: ["tubing_pressure"])),
            "production", solveTrack,
            LegacyTitle: "Production Forecast — Daily Oil Rate, bopd (Demo)",
            TitleAr: "معدّل الإنتاج اليومي — توقّع التدفّق",
            DescriptionAr: "توقّع إنتاج البئر اليومي من النفط (برميل/يوم) من قياس الخانق، وضغط رأس الأنبوب، ونسبة الماء، ومعدّل الغاز، وعدد أيام التشغيل. مسألة الانحدار اليومية في أي مكتب إنتاج، ومكان جيد لتكتشف كم تساوي هندسة الخصائص فعلاً. التقييم بجذر متوسط مربّع الخطأ (الأقل أفضل)."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            ProductionForecastTitle,
            "Now do it across time. From a well's daily choke, tubing-head pressure, water cut and gas rate, "
            + "forecast the daily oil rate (bopd) for the NEXT 90 days — the evaluation set is the future, not "
            + "a random hold-out. Use a Chronological split node (order by the date column) so your model "
            + "trains on the past; a random split leaks later days into training and flatters you right up "
            + "until you submit. Scored on RMSE (lower is better). Some days the water-cut sample was not "
            + "taken — a gap in a time series is its own kind of problem, since the row either side of it "
            + "still knows roughly what it should have been." + Synthetic,
            "rmse", "oil_rate", "Forecasting",
            Dirtied(BuildProductionForecast(), 20260210, "oil_rate", new Mess(
                Gaps: ["water_cut", "gas_rate"])),
            "production", solveTrack,
            LegacyTitle: "Production Decline — Forecast the Next 90 Days (Demo)",
            TitleAr: "انحدار الإنتاج — تنبّأ بالتسعين يوماً القادمة",
            DescriptionAr: "الآن افعلها عبر الزمن. من قياسات البئر اليومية، تنبّأ بمعدّل النفط اليومي لـ**التسعين يوماً القادمة** — مجموعة التقييم هي المستقبل، لا عيّنة عشوائية. استخدم عقدة التقسيم الزمني ليتدرّب نموذجك على الماضي؛ فالتقسيم العشوائي يسرّب أياماً لاحقة إلى التدريب ويجاملك حتى لحظة التسليم. التقييم بجذر متوسط مربّع الخطأ."));

        // ---- Maintenance & Reliability ----------------------------------------------------------

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            EspTitle,
            "Service the pump before it stops. From intake pressure, motor temperature, vibration, current, "
            + "flow rate and runtime hours, flag the electric submersible pumps likely to fail — so the work "
            + "goes on the plan instead of into an emergency call-out. Binary classification, scored on "
            + "accuracy. Motor temperature is logged in Celsius by some crews and Fahrenheit by others, "
            + "with `motor_temp_unit` recording which — and since a hot motor is most of the signal, "
            + "leaving that unconverted will cost you more here than anywhere else."
            + Synthetic + Dirty,
            "accuracy", "failure", "BinaryClassification",
            Dirtied(BuildEsp(), 20260211, "failure", new Mess(
                Gaps: ["vibration", "flow_rate"],
                Sentinels: ["intake_pressure", "current"],
                Units: new UnitSwitch("motor_temp", "motor_temp_unit", "C", "F", c => (c * 9 / 5) + 32, 0.4))),
            "maintenance", solveTrack,
            LegacyTitle: "ESP Pump Failure — Predictive Maintenance (Demo)",
            TitleAr: "أعطال المضخّات الغاطسة — الصيانة التنبّؤية",
            DescriptionAr: "اخدم المضخّة قبل أن تتوقّف. من ضغط المدخل، وحرارة المحرّك، والاهتزاز، والتيار، ومعدّل التدفّق، وساعات التشغيل، حدّد المضخّات المرشّحة للعطل — لتذهب الصيانة إلى الخطة بدل نداء الطوارئ. حرارة المحرّك مسجّلة بالمئوية لدى بعض الفرق وبالفهرنهايت لدى غيرها، وهي معظم الإشارة: إهمال التحويل يكلّفك هنا أكثر من أي مكان آخر. تصنيف ثنائي، والتقييم بالدقّة."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            AnomalyTitle,
            "The unsupervised one — no labels to train on at all. A healthy pump's five sensors move together "
            + "because one load drives them all; a fault breaks that pattern by pushing a single sensor off "
            + "where the others say it should be. Train on normal history only, then score every evaluation "
            + "row for how abnormal it looks. The true labels exist solely to rank you. Scored on AUC (higher "
            + "is better), because with one anomaly in six, a model that flags nothing would still be 83% "
            + "'accurate'. This extract is clean — the only thing out of place in it is a genuine fault."
            + Synthetic,
            // Deliberately not dirtied. Every other challenge learns a labelled relationship and can shrug
            // off a few bad cells; this one learns the SHAPE of normal, and a -999 is by construction the
            // most abnormal row in the file. Injected mess would not be a cleaning exercise here, it would
            // be the answer — the model would rank the dirt top and score well for finding nothing real.
            "auc", "label", "AnomalyDetection", BuildAnomaly(), "maintenance", solveTrack,
            LegacyTitle: "ESP Fault Watch — Flag Abnormal Sensor Rows (Demo)",
            TitleAr: "رصد أعطال المضخّات — التقط القراءات الشاذّة",
            DescriptionAr: "التحدي غير المُوجَّه: لا تسميات تتدرّب عليها إطلاقاً. حسّاسات المضخّة السليمة تتحرّك معاً لأن حِملاً واحداً يقودها جميعاً؛ والعطل يكسر هذا النمط بدفع حسّاس واحد بعيداً عمّا تقوله البقية. تدرّب على التاريخ الطبيعي وحده، ثم أعطِ كل صف درجة شذوذ. التقييم بمساحة تحت المنحنى (الأعلى أفضل)، لأن نموذجاً لا يشير إلى شيء سيبدو «دقيقاً» بنسبة ٨٣٪ مع شذوذ واحد من كل ستة."));

        // ---- Facilities & Plant -----------------------------------------------------------------

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            CorrosionTitle,
            "Rank the pipework for the inspection plan. From wall-loss history, water cut, CO₂ partial "
            + "pressure, H₂S, flow velocity, temperature, age and coating condition, sort each line into low, "
            + "medium or high corrosion risk. Watch the slow lines: below about 1 m/s the water drops out and "
            + "sits on the steel, which is why velocity matters in a direction people find counter-intuitive. "
            + "Three classes, scored on accuracy. Temperatures come off two systems — °C and °F, per "
            + "`temperature_unit` — and H₂S is only sampled on some lines." + Synthetic + Dirty,
            "accuracy", "risk", "MulticlassClassification",
            Dirtied(BuildCorrosionRisk(), 20260212, "risk", new Mess(
                Gaps: ["h2s_ppm", "flow_velocity"],
                Sentinels: ["wall_loss_pct"],
                Units: new UnitSwitch("temperature", "temperature_unit", "C", "F", c => (c * 9 / 5) + 32, 0.33))),
            "facilities", solveTrack,
            TitleAr: "مخاطر تآكل خطوط الأنابيب — منخفض أم متوسط أم مرتفع",
            DescriptionAr: "رتّب الخطوط لخطة التفتيش. من تاريخ فقد سماكة الجدار، ونسبة الماء، والضغط الجزئي لثاني أكسيد الكربون، وكبريتيد الهيدروجين، وسرعة التدفّق، والحرارة، والعمر، وحالة الطلاء، صنّف كل خط إلى خطر منخفض أو متوسط أو مرتفع. انتبه للخطوط البطيئة: تحت متر واحد في الثانية تقريباً ينفصل الماء ويستقرّ على الفولاذ. ثلاثة أصناف، والتقييم بالدقّة."));

        // ---- HSE --------------------------------------------------------------------------------

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            NearMissTitle,
            "A near miss is a free lesson — if you read it right. From the reported circumstances (energy "
            + "source, working height, load, PPE compliance, shift, hours into the shift, crew), classify the "
            + "potential severity had it not been a near miss: minor, serious or major. This is how a safety "
            + "team decides which of a hundred reports to investigate first. Three classes, scored on "
            + "accuracy. Not every column carries signal — finding the ones that do not is part of the work. "
            + "And near-miss reports are written by people at the end of a shift, so the fields they could "
            + "not be bothered with are blank." + Synthetic + Dirty,
            "accuracy", "severity", "MulticlassClassification",
            Dirtied(BuildNearMissSeverity(), 20260213, "severity", new Mess(
                Gaps: ["load_kg", "hours_into_shift", "crew_size"],
                Sentinels: ["height_m"])),
            "hse", solveTrack,
            TitleAr: "شدّة الحوادث الوشيكة — إلى أي مدى كان يمكن أن تسوء؟",
            DescriptionAr: "الحادث الوشيك درس مجاني، إن أحسنت قراءته. من ظروف البلاغ — مصدر الطاقة، وارتفاع العمل، والحمل، والالتزام بمعدّات الوقاية، والوردية، وساعات العمل المنقضية، وحجم الطاقم — صنّف الشدّة المحتملة لو لم يكن وشيكاً: طفيفة أو جسيمة أو كبرى. هكذا يقرّر فريق السلامة أي البلاغات يحقّق فيه أولاً. وليست كل الأعمدة ذات دلالة — واكتشاف ما لا يفيد جزء من العمل."));

        // ---- Medical & Health (Ahmadi Hospital) --------------------------------------------------

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            DiabetesTitle,
            "Who should be called back for a confirmatory HbA1c? From a routine occupational-health panel — "
            + "age, BMI, waist, fasting glucose, triglycerides, HDL, blood pressure, family history and "
            + "activity level — predict whose HbA1c will come back elevated. Note what is NOT in the "
            + "features: the HbA1c itself. That is the test you are trying to target, and predicting it from "
            + "cheaper routine bloods is the whole point. Binary classification, scored on accuracy. A "
            + "screening aid for prioritising follow-up, not a diagnosis. "
            + "One trap worth naming: fasting glucose arrives in **two units** — mg/dL from the older "
            + "analyser, mmol/L from the newer one, per `fasting_glucose_unit`. They differ by a factor of "
            + "eighteen, so a model handed the raw column sees a bimodal mess where the strongest single "
            + "predictor should be. Not every panel is complete, either — lipids in particular are only "
            + "ordered when somebody thought to." + SyntheticClinical + Dirty,
            "accuracy", "screen_positive", "BinaryClassification",
            Dirtied(BuildDiabetesScreening(), 20260214, "screen_positive", new Mess(
                Gaps: ["triglycerides", "hdl", "waist_cm"],
                Sentinels: ["systolic_bp"],
                Units: new UnitSwitch("fasting_glucose", "fasting_glucose_unit", "mg/dL", "mmol/L", mg => mg / 18.0182, 0.4))),
            "medical", solveTrack,
            TitleAr: "الكشف عن السكري من تحليل مخبري روتيني",
            DescriptionAr: "مَن ينبغي استدعاؤه لفحص السكر التراكمي التأكيدي؟ من لوحة الصحة المهنية الروتينية — العمر، ومؤشر كتلة الجسم، ومحيط الخصر، وسكر الصيام، والدهون الثلاثية، والكوليسترول النافع، وضغط الدم، والتاريخ العائلي، ومستوى النشاط — توقّع مَن سيعود تحليله التراكمي مرتفعاً. لاحظ ما **ليس** بين الخصائص: التحليل التراكمي نفسه؛ فالتنبّؤ به من تحاليل أرخص هو جوهر المسألة. وانتبه: سكر الصيام يصل بوحدتين مختلفتين بينهما فارق ثمانية عشر ضعفاً. أداة ترجيح للمتابعة، وليست تشخيصاً. البيانات مُولّدة ولا تتضمّن أي سجلات مرضى."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            LabQcTitle,
            "Quality control for the chemistry analyser, with no labelled failures to learn from. Every "
            + "morning the lab runs a known control sample; sodium, potassium, chloride, glucose and "
            + "creatinine all shift together as the instrument's calibration wanders, and that is fine. What "
            + "is not fine is one channel drifting alone — a tired electrode, a new reagent lot, a bad "
            + "calibration — because every patient result on that run inherits the error. Train on in-control "
            + "history, then score each run for how far it sits off the pattern. Scored on AUC (higher is "
            + "better). This extract is clean: on a QC file, the only thing that should be out of place is "
            + "the failure you are hunting." + SyntheticClinical,
            // Not dirtied, for the same reason as the ESP fault watch: injected junk would be the most
            // abnormal thing in an unsupervised file, and finding it would score as success.
            "auc", "label", "AnomalyDetection", BuildLabQc(), "medical", solveTrack,
            TitleAr: "ضبط جودة المحلّل المخبري — التقط التشغيلة المنحرفة",
            DescriptionAr: "ضبط جودة بلا أعطال موسومة للتعلّم منها. كل صباح يشغّل المختبر عيّنة ضبط معروفة، فتتحرّك الصوديوم والبوتاسيوم والكلوريد والجلوكوز والكرياتينين معاً مع تغيّر معايرة الجهاز، وهذا مقبول. غير المقبول أن ينحرف قناة واحدة بمفردها — قطب متعب، أو دفعة كواشف جديدة — لأن كل نتيجة مريض في تلك التشغيلة ترث الخطأ. تدرّب على التاريخ المنضبط، ثم قِس بُعد كل تشغيلة عن النمط. التقييم بمساحة تحت المنحنى. البيانات مُولّدة ولا تتضمّن أي سجلات مرضى."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            TriageTitle,
            "At the emergency department door, from the observations taken in the first two minutes — heart "
            + "rate, blood pressure, respiratory rate, oxygen saturation, temperature, pain score, age and "
            + "whether they arrived by ambulance — assign the acuity level: resuscitation, emergent, urgent or "
            + "standard. The hard part is the class balance: resuscitation cases are rare, and a model that "
            + "never predicts them can still look good on overall accuracy. Four classes, scored on accuracy. "
            + "A decision aid for a trained triage nurse, never a replacement for one. "
            + "Expect holes: when somebody arrives in a bad way, a temperature and a pain score are not the "
            + "first priority, so the sickest patients are the likeliest to be missing exactly the readings "
            + "you wanted. Dropping incomplete rows here would quietly throw away the cases that matter most."
            + SyntheticClinical + Dirty,
            "accuracy", "acuity", "MulticlassClassification",
            Dirtied(BuildTriage(), 20260215, "acuity", new Mess(
                Gaps: ["temperature", "pain_score"],
                Sentinels: ["spo2"])),
            "medical", solveTrack,
            TitleAr: "فرز الطوارئ — حدّد مستوى الحدّة",
            DescriptionAr: "عند باب الطوارئ، ومن القياسات المأخوذة في أول دقيقتين — النبض، وضغط الدم، ومعدّل التنفّس، وتشبّع الأكسجين، والحرارة، ودرجة الألم، والعمر، وطريقة الوصول — حدّد مستوى الحدّة: إنعاش، أو طارئ، أو عاجل، أو اعتيادي. الصعوبة في اختلال توازن الأصناف: حالات الإنعاش نادرة، ونموذج لا يتنبّأ بها أبداً قد يبدو جيداً في الدقّة الإجمالية. وتوقّع نقصاً في البيانات: أشدّ المرضى حالاً هم الأرجح أن تنقصهم القراءات. أداة مساندة لممرّض فرز مدرّب، لا بديلاً عنه. البيانات مُولّدة ولا تتضمّن أي سجلات مرضى."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            StayTitle,
            "How many days will this admission need? From age, admission type, comorbidity count, whether "
            + "surgery is planned, prior admissions, and the bloods taken on arrival (creatinine, albumin, "
            + "white cell count), predict length of stay in days. Beds, staffing and discharge planning all "
            + "run on this number. Albumin is the quiet one to watch — it says more about how someone will "
            + "recover than its place on the form suggests. Regression, scored on RMSE (lower is better). "
            + "Creatinine comes in µmol/L or mg/dL depending on which lab reported it (`creatinine_unit`), "
            + "and albumin is missing wherever nobody ordered it — which is, inconveniently, most of the "
            + "elective admissions." + SyntheticClinical + Dirty,
            "rmse", "los_days", "Regression",
            Dirtied(BuildLengthOfStay(), 20260216, "los_days", new Mess(
                Gaps: ["albumin", "white_cell_count"],
                Sentinels: ["creatinine"],
                Units: new UnitSwitch("creatinine", "creatinine_unit", "umol/L", "mg/dL", umol => umol / 88.42, 0.35))),
            "medical", solveTrack,
            TitleAr: "مدّة الإقامة — كم يوماً في المستشفى؟",
            DescriptionAr: "كم يوماً يحتاج هذا الدخول؟ من العمر، ونوع الدخول، وعدد الأمراض المصاحبة، ووجود جراحة مقرّرة، والدخولات السابقة، وتحاليل الوصول — الكرياتينين والألبومين وتعداد الكريات البيض — توقّع مدّة الإقامة بالأيام. الأَسِرّة والملاك وخطط الخروج كلها تُبنى على هذا الرقم. والألبومين هو المؤشر الهادئ الجدير بالانتباه. انحدار، والتقييم بجذر متوسط مربّع الخطأ. البيانات مُولّدة ولا تتضمّن أي سجلات مرضى."));

        // ---- Training & Development -------------------------------------------------------------

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            CourseTitle,
            "T&CD's own problem, and the reason this challenge exists: a seat booked is not a course "
            + "finished. From what is known the day someone enrols — grade and tenure, the delivery mode, "
            + "how long the course runs, how far ahead they booked, what they have completed before, and "
            + "how loaded their department is that quarter — predict who will not complete. "
            + "The point is not to judge anybody. It is to know in week one who needs a nudge, instead of "
            + "finding out in week six that a seat was wasted. Binary classification, scored on accuracy. "
            + "Watch the course length: some records are in days and some in hours, and `length_unit` is "
            + "the only thing that says which." + Synthetic + Dirty,
            "accuracy", "completed", "BinaryClassification",
            Dirtied(BuildCourseCompletion(), 20260217, "completed", new Mess(
                Gaps: ["prior_courses", "workload_index"],
                Sentinels: ["days_booked_ahead"],
                Units: new UnitSwitch("course_length", "length_unit", "days", "hours", d => d * 7.5, 0.35))),
            "training", starterTrack,
            TitleAr: "إتمام الدورات — مَن لن يُكمل؟",
            DescriptionAr: "مسألة إدارة التدريب نفسها: حجز مقعد لا يعني إتمام الدورة. مما يُعرف يوم التسجيل — "
            + "الدرجة ومدة الخدمة، وأسلوب التقديم، وطول الدورة، وكم سجّل مبكراً، وما أتمّه سابقاً، وحجم "
            + "الضغط على إدارته ذلك الربع — توقّع مَن لن يُكمل. والغرض ليس الحكم على أحد، بل معرفة مَن يحتاج "
            + "إلى متابعة في الأسبوع الأول بدل اكتشاف مقعد ضائع في الأسبوع السادس. تصنيف ثنائي، والتقييم "
            + "بالدقّة. وانتبه لطول الدورة: بعض السجلات بالأيام وبعضها بالساعات، وعمود الوحدة وحده يميّزها."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            DemandTitle,
            "How many seats to open, and where. Book too few and people wait a quarter for a course they "
            + "needed last month; book too many and an instructor teaches an empty room. From the course "
            + "family, the quarter, the size of the discipline it serves, how many certificates fall due "
            + "for renewal, what last year's intake was, and whether a campaign is pushing it, predict the "
            + "number of seats that will actually be taken. Regression, scored on RMSE (lower is better). "
            + "Two things sit in the data that a spreadsheet average misses: the summer quarter empties "
            + "out, and a renewal wave lifts demand far more than headcount alone would suggest."
            + Synthetic + Dirty,
            "rmse", "seats_taken", "Regression",
            Dirtied(BuildCourseDemand(), 20260218, "seats_taken", new Mess(
                Gaps: ["renewals_due", "campaign"],
                Sentinels: ["last_year_seats"])),
            "training", starterTrack,
            TitleAr: "الطلب على الدورات — كم مقعدًا في الربع القادم؟",
            DescriptionAr: "كم مقعدًا نفتح، وأين. إن قلّت المقاعد انتظر الموظف ربعًا كاملًا لدورة احتاجها "
            + "الشهر الماضي، وإن زادت درّس المدرّب قاعة فارغة. من عائلة الدورة، والربع، وحجم التخصص الذي "
            + "تخدمه، وعدد الشهادات المستحقّة للتجديد، وأعداد العام الماضي، ووجود حملة ترويجية من عدمه — "
            + "توقّع عدد المقاعد التي ستُشغل فعلًا. انحدار، والتقييم بجذر متوسط مربّع الخطأ (الأقل أفضل). "
            + "وفي البيانات أمران يفوتان المتوسط الحسابي: ربع الصيف يخلو تقريبًا، وموجة التجديد ترفع الطلب "
            + "أكثر بكثير مما يوحي به عدد الموظفين وحده."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            CompetencyTitle,
            "A new engineer joins a competency programme. When will the assessor sign them off? "
            + "From prior experience, qualification, how much of the programme is on-the-job, the mentor's "
            + "load, how often a rotation is actually available, and the assessments already passed, "
            + "predict the number of months to sign-off. Regression, scored on RMSE (lower is better). "
            + "The answer T&CD wants out of this is not a number per person — it is which of those levers "
            + "moves the date most, because that is the one worth spending money on. "
            + "Mentor load is the one that surprises people." + Synthetic + Dirty,
            "rmse", "months_to_signoff", "Regression",
            Dirtied(BuildTimeToCompetency(), 20260219, "months_to_signoff", new Mess(
                Gaps: ["on_job_pct", "rotation_availability"],
                Sentinels: ["assessments_passed"],
                Units: new UnitSwitch("programme_length", "length_unit", "months", "weeks", m => m * 4.35, 0.3))),
            "training", starterTrack,
            TitleAr: "الزمن حتى الكفاءة — متى يأتي الاعتماد؟",
            DescriptionAr: "مهندس جديد يلتحق ببرنامج كفاءة. متى يعتمده المقيّم؟ من الخبرة السابقة، والمؤهل، "
            + "ونسبة التدريب على رأس العمل، وحِمل المرشد، ومدى توفّر فرص التدوير، والتقييمات المجتازة — "
            + "توقّع عدد الأشهر حتى الاعتماد. انحدار، والتقييم بجذر متوسط مربّع الخطأ (الأقل أفضل). "
            + "وما تريده إدارة التدريب من هذا ليس رقمًا لكل شخص، بل معرفة أي عامل يقرّب الموعد أكثر، لأنه "
            + "العامل الذي يستحق الإنفاق عليه. وحِمل المرشد هو المفاجأة عادةً. "
            + "وانتبه لطول البرنامج: بعض السجلات بالأشهر وبعضها بالأسابيع."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            NextTrackTitle,
            "Recommend the next learning track for a colleague, the way a good supervisor would. "
            + "From their discipline, grade, what they have already completed, how they scored, the gap "
            + "between their role and the next one, and whether a certificate is due, choose one of four: "
            + "**foundations**, **data handling**, **modelling**, or **leading a team**. "
            + "Multiclass classification, scored on accuracy. "
            + "There is no single right answer for a person — but there is a clear pattern across hundreds "
            + "of them, and that pattern is what a recommendation engine on this platform would run on."
            + Synthetic + Dirty,
            "accuracy", "next_track", "MulticlassClassification",
            Dirtied(BuildNextTrack(), 20260220, "next_track", new Mess(
                Gaps: ["role_gap"],
                Sentinels: ["grade"])),
            "training", starterTrack,
            TitleAr: "ما الذي ينبغي أن يتعلّمه بعد ذلك؟",
            DescriptionAr: "اقترح المسار التعليمي التالي لزميل، كما يفعل المشرف الجيّد. من تخصصه، ودرجته، "
            + "وما أتمّه سابقًا، ودرجاته، والفجوة بين دوره الحالي والدور التالي، واستحقاق شهادة من عدمه — "
            + "اختر واحدًا من أربعة: **الأساسيات**، أو **التعامل مع البيانات**، أو **بناء النماذج**، أو "
            + "**قيادة فريق**. تصنيف متعدّد الفئات، والتقييم بالدقّة. "
            + "لا توجد إجابة واحدة صحيحة لشخص بعينه، لكن يوجد نمط واضح عبر المئات، وهذا النمط هو ما "
            + "سيعمل عليه محرّك التوصية في هذه المنصة."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            RatingTitle,
            "Predict a poor course rating **before the session runs**, from how it was set up rather than "
            + "who is teaching it: class size, room or online, the time of day and where it falls in the "
            + "week, how old the material is, how many attendees arrive without the prerequisite, and how "
            + "long since the outline was revised. Binary classification, scored on accuracy. "
            + "This one is about scheduling and design, not about instructors — every field in the data is "
            + "something T&CD can change with a booking system, which is exactly the point: a session "
            + "flagged in advance can be fixed while it is still only a diary entry."
            + Synthetic + Dirty,
            "accuracy", "rated_poorly", "BinaryClassification",
            Dirtied(BuildCourseRating(), 20260221, "rated_poorly", new Mess(
                Gaps: ["material_age_months", "prereq_missing_pct"],
                Sentinels: ["class_size"])),
            "training", starterTrack,
            TitleAr: "تقييم الدورة — هل ستخيّب هذه الجلسة الظن؟",
            DescriptionAr: "توقّع التقييم الضعيف **قبل انعقاد الجلسة**، من طريقة إعدادها لا ممّن يقدّمها: "
            + "حجم المجموعة، وحضوري أم عن بُعد، وتوقيت اليوم وموقعه من الأسبوع، وعمر المادة العلمية، "
            + "ونسبة الحاضرين دون المتطلب السابق، والمدة منذ آخر تحديث للمحتوى. تصنيف ثنائي، والتقييم "
            + "بالدقّة. وهذا التحدي عن الجدولة والتصميم لا عن المدرّبين: كل حقل في البيانات شيء تستطيع "
            + "إدارة التدريب تغييره من نظام الحجز، وهذا هو المقصود تحديدًا — الجلسة التي يُنبَّه إليها "
            + "مبكرًا يمكن إصلاحها وهي ما تزال موعدًا في التقويم."));

        // ---- People & HR ------------------------------------------------------------------------

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            PayrollTitle,
            "Find the payroll run that is wrong, with no examples of wrong to learn from. In a healthy "
            + "run the figures move together — hours drive base pay, overtime tracks hours, allowances and "
            + "deductions follow grade — and a whole month shifting up is just a pay award, which is fine. "
            + "What is not fine is one component breaking step: a duplicated payment, a rate applied at ten "
            + "times its value, overtime that could not physically have been worked. Train on clean months, "
            + "then score each run for how far it sits off the pattern. Scored on AUC (higher is better). "
            + "Note what the totals will not save you: every broken run still adds up, because the net was "
            + "recomputed from the wrong parts. This challenge judges **records, not people** — there is "
            + "nothing in it about any individual's performance, and it is built that way on purpose."
            + Synthetic,
            // Not dirtied: like the other unsupervised challenges, injected junk would be the most
            // abnormal thing in the file and finding it would score as success.
            "auc", "label", "AnomalyDetection", BuildPayroll(), "people", solveTrack,
            TitleAr: "سلامة الرواتب — التقط التشغيلة الخاطئة",
            DescriptionAr: "اعثر على تشغيلة الرواتب الخاطئة، دون أي أمثلة على الخطأ تتعلّم منها. في التشغيلة "
            + "السليمة تتحرّك الأرقام معاً — الساعات تقود الراتب الأساسي، والعمل الإضافي يتبع الساعات، "
            + "والبدلات والاستقطاعات تتبع الدرجة — وارتفاع الشهر كله مجرّد علاوة، وهذا طبيعي. غير الطبيعي أن "
            + "يخرج بند واحد عن السرب: دفعة مكرّرة، أو معدّل طُبّق بعشرة أضعافه، أو عمل إضافي يستحيل أداؤه. "
            + "تدرّب على الأشهر السليمة، ثم قِس بُعد كل تشغيلة عن النمط. التقييم بمساحة تحت المنحنى. "
            + "ولاحظ أن المجاميع لن تنقذك: كل تشغيلة خاطئة تبقى متوازنة حسابياً. "
            + "هذا التحدي يحكم على **السجلات لا على الأشخاص**، وقد بُني هكذا عن قصد."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            AbsenceTitle,
            "How many working days will a site lose to absence next month? Asked of the **site**, never of "
            + "a person: the data has no individual in it, only team size, shift pattern, the month, how "
            + "much annual leave is already approved, whether a shutdown is planned, the seasonal illness "
            + "index, and last month's figure. Regression, scored on RMSE (lower is better). "
            + "Get this right and resourcing stops being a monthly scramble — the relief crew is booked "
            + "before the gap opens rather than after. Watch the shutdown months; they behave nothing like "
            + "the rest of the year." + Synthetic + Dirty,
            "rmse", "days_lost", "Regression",
            Dirtied(BuildAbsence(), 20260222, "days_lost", new Mess(
                Gaps: ["illness_index", "approved_leave_days"],
                Sentinels: ["last_month_days_lost"])),
            "people", starterTrack,
            TitleAr: "تخطيط الغياب — كم يومًا سيفقده الموقع؟",
            DescriptionAr: "كم يوم عمل سيفقده الموقع بسبب الغياب الشهر القادم؟ والسؤال عن **الموقع** لا عن "
            + "شخص: لا يوجد في البيانات أي فرد، بل حجم الفريق، ونمط الورديات، والشهر، وأيام الإجازات "
            + "المعتمدة مسبقًا، ووجود إيقاف مخطّط من عدمه، ومؤشّر الأمراض الموسمية، ورقم الشهر الماضي. "
            + "انحدار، والتقييم بجذر متوسط مربّع الخطأ (الأقل أفضل). وإذا أصبت في هذا لم يعد توفير الكوادر "
            + "سباقًا شهريًا: يُحجز طاقم التغطية قبل أن تنشأ الفجوة لا بعدها. وانتبه لأشهر الإيقاف، فهي "
            + "لا تشبه بقية العام في شيء."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            HireTitle,
            "A requisition is raised. How many days until somebody starts? From the grade, the discipline, "
            + "contract type, how many approval steps it must pass, how scarce the skill is in the market, "
            + "how many applicants it drew, and the month it opened, predict the days to fill. "
            + "Regression, scored on RMSE (lower is better). "
            + "The interesting result here is not the prediction but what it exposes: approvals and market "
            + "scarcity pull in the same direction, and a well-advertised post with a long approval chain "
            + "still ends up slow. The number of applicants matters far less than people expect."
            + Synthetic + Dirty,
            "rmse", "days_to_fill", "Regression",
            Dirtied(BuildTimeToHire(), 20260223, "days_to_fill", new Mess(
                Gaps: ["applicants", "month_opened"],
                Sentinels: ["grade"])),
            "people", starterTrack,
            TitleAr: "زمن التوظيف — متى يُشغَل الشاغر؟",
            DescriptionAr: "يُفتح طلب توظيف. كم يومًا حتى يباشر أحدهم العمل؟ من الدرجة، والتخصص، ونوع العقد، "
            + "وعدد خطوات الاعتماد، وندرة المهارة في السوق، وعدد المتقدّمين، وشهر فتح الطلب — توقّع عدد "
            + "الأيام حتى الإشغال. انحدار، والتقييم بجذر متوسط مربّع الخطأ (الأقل أفضل). "
            + "والنتيجة المثيرة هنا ليست التنبؤ بل ما يكشفه: خطوات الاعتماد وندرة السوق تدفعان في الاتجاه "
            + "نفسه، والوظيفة المعلَنة جيدًا مع سلسلة اعتماد طويلة تبقى بطيئة. أما عدد المتقدّمين فأثره "
            + "أقل بكثير مما يُتوقَّع."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            OvertimeTitle,
            "Which teams will breach their overtime cap this month? A **team-level** question, decided "
            + "before the month starts rather than explained after it ends: headcount against planned "
            + "workload, vacancies left open, overlapping leave, whether a shutdown or turnaround falls in "
            + "the period, the shift pattern, and the last three months of overtime. "
            + "Binary classification, scored on accuracy. "
            + "Nothing in this data identifies a person, and nothing in it should. The finding worth "
            + "having is that vacancies and leave overlap compound — either alone is survivable."
            + Synthetic + Dirty,
            "accuracy", "over_cap", "BinaryClassification",
            Dirtied(BuildOvertime(), 20260224, "over_cap", new Mess(
                Gaps: ["vacancies", "leave_overlap_days"],
                Sentinels: ["planned_workload"])),
            "people", solveTrack,
            TitleAr: "سقف العمل الإضافي — أي الفرق سيتجاوزه؟",
            DescriptionAr: "أي الفرق ستتجاوز سقف العمل الإضافي هذا الشهر؟ سؤال على مستوى **الفريق**، يُحسم "
            + "قبل بدء الشهر لا يُفسَّر بعد انتهائه: عدد الموظفين مقابل حجم العمل المخطّط، والشواغر غير "
            + "المشغولة، وتداخل الإجازات، ووقوع إيقاف أو عمرة في الفترة، ونمط الورديات، وعمل الأشهر "
            + "الثلاثة الماضية الإضافي. تصنيف ثنائي، والتقييم بالدقّة. "
            + "ولا شيء في هذه البيانات يحدّد شخصًا، ولا ينبغي أن يكون. والنتيجة الجديرة بالاهتمام أن "
            + "الشواغر وتداخل الإجازات يتضاعف أثرهما معًا، بينما يمكن احتمال كل منهما وحده."));

        await MaybeAddAsync(db, artifacts, stamp, ct, new CompetitionSpec(
            GradeTitle,
            "Job evaluation, done consistently. Given a role's attributes — span of control, budget "
            + "responsibility, required qualification and experience, technical depth, safety-critical "
            + "duties and how much of the work is offshore — place it in one of four grade bands: "
            + "**support**, **professional**, **senior** or **lead**. "
            + "Multiclass classification, scored on accuracy. "
            + "This is about the **role**, not the person holding it. The value is consistency: two roles "
            + "with the same weight should land in the same band whichever committee looked at them, and "
            + "a model trained on past evaluations is a fair way to test whether they did."
            + Synthetic + Dirty,
            "accuracy", "grade_band", "MulticlassClassification",
            Dirtied(BuildGradeBand(), 20260225, "grade_band", new Mess(
                Gaps: ["budget_kd", "technical_depth"],
                Sentinels: ["direct_reports"])),
            "people", solveTrack,
            TitleAr: "تقييم الوظائف — إلى أي فئة تنتمي هذه الوظيفة؟",
            DescriptionAr: "تقييم الوظائف، بصورة متّسقة. من خصائص الوظيفة — نطاق الإشراف، والمسؤولية "
            + "المالية، والمؤهل والخبرة المطلوبان، والعمق الفني، والمهام الحرجة للسلامة، ونسبة العمل "
            + "البحري — ضعها في واحدة من أربع فئات: **مساندة**، أو **مهنية**، أو **أولى**، أو **قيادية**. "
            + "تصنيف متعدّد الفئات، والتقييم بالدقّة. "
            + "والحديث هنا عن **الوظيفة** لا عن شاغلها. والقيمة في الاتّساق: وظيفتان بالوزن نفسه ينبغي أن "
            + "تقعا في الفئة نفسها أيًّا كانت اللجنة التي نظرت فيهما، والنموذج المدرَّب على تقييمات سابقة "
            + "طريقة عادلة لاختبار ما إذا كان ذلك قد تحقّق."));

        await db.SaveChangesAsync(ct);
    }

    private static Task<Guid?> TrackId(KocDbContext db, string title, CancellationToken ct) =>
        db.Set<Domain.Learning.LearningTrack>().Where(t => t.Title == title).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);

    private sealed record CompetitionSpec(
        string Title, string Description, string Scorer, string LabelColumn, string TaskType,
        (string Training, string Evaluation, string AnswerKey) Data, string? CategoryCode = null,
        Guid? RecommendedTrackId = null, bool IsFeatured = false, string? LegacyTitle = null,
        string? TitleAr = null, string? DescriptionAr = null);

    private static async Task MaybeAddAsync(KocDbContext db, IArtifactService artifacts, DateTime stamp, CancellationToken ct, CompetitionSpec spec)
    {
        var existing = await db.Set<Competition>().FirstOrDefaultAsync(c => c.Title == spec.Title, ct);

        // An earlier release shipped several of these with "(Demo)" in the title, from when they were a
        // development-only seed. Carry that row forward instead of standing a second copy beside it —
        // its submissions and leaderboard belong to the same challenge.
        if (existing is null && spec.LegacyTitle is { Length: > 0 })
        {
            existing = await db.Set<Competition>().FirstOrDefaultAsync(c => c.Title == spec.LegacyTitle, ct);
            if (existing is not null)
            {
                existing.Title = spec.Title;
                existing.Description = spec.Description;
            }
        }

        if (existing is not null)
        {
            // Fill in what a row predating the field never had; never overwrite a choice already made.
            existing.CategoryCode ??= spec.CategoryCode;
            await EnsureArabicAsync(db, existing, spec, stamp, ct);
            return;
        }

        var slug = Slug(spec.Title);
        var trainingRef = await Save(artifacts, spec.Data.Training, $"{slug}/training.csv", ct);
        var evalRef = await Save(artifacts, spec.Data.Evaluation, $"{slug}/evaluation.csv", ct);
        var keyRef = await Save(artifacts, spec.Data.AnswerKey, $"{slug}/answer-key.csv", ct);

        var competition = new Competition
        {
            Title = spec.Title,
            Description = spec.Description,
            Status = "active",
            VisibilityScope = VisibilityScope.Company,
            VisibilityOrgUnitId = Guid.Empty,
            SubmissionQuotaPerDay = 25,
            ScorerCode = spec.Scorer,
            TrainingDatasetArtifactId = trainingRef.Id,
            EvaluationArtifactId = evalRef.Id,
            AnswerKeyArtifactId = keyRef.Id,
            LabelColumn = spec.LabelColumn,
            IdColumn = "id",
            TaskType = spec.TaskType,
            CategoryCode = spec.CategoryCode,
            RecommendedTrackId = spec.RecommendedTrackId,
            IsFeatured = spec.IsFeatured,
            CreatedByUserId = Owner,
            CreatedUtc = stamp,
        };

        db.Set<Competition>().Add(competition);
        await EnsureArabicAsync(db, competition, spec, stamp, ct);
    }

    /// <summary>
    /// Adds the Arabic for a seeded competition if it is not already there.
    /// <para>
    /// Competitions are translated by <b>id</b>, not by a stable code, so this cannot live in
    /// <c>ContentTranslationSeeder</c> beside the categories and badges — the key does not exist until
    /// the row does. Per-item idempotent and never overwriting, the same bargain the rest make: once an
    /// administrator or the author has corrected a translation, a later release leaves it alone.
    /// </para>
    /// </summary>
    private static async Task EnsureArabicAsync(
        KocDbContext db, Competition competition, CompetitionSpec spec, DateTime stamp, CancellationToken ct)
    {
        var key = competition.Id.ToString();

        foreach (var (field, text) in new[]
                 {
                     (Domain.Localization.TranslatedContent.Name, spec.TitleAr),
                     (Domain.Localization.TranslatedContent.Description, spec.DescriptionAr),
                 })
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var already = await db.ContentTranslations.AnyAsync(
                t => t.EntityType == Domain.Localization.TranslatedContent.Competition
                     && t.EntityKey == key
                     && t.Field == field
                     && t.Language == Contracts.Localization.KocLanguages.Arabic, ct);

            if (already)
            {
                continue;
            }

            db.ContentTranslations.Add(new Domain.Localization.ContentTranslation
            {
                EntityType = Domain.Localization.TranslatedContent.Competition,
                EntityKey = key,
                Field = field,
                Language = Contracts.Localization.KocLanguages.Arabic,
                Text = text,
                CreatedUtc = stamp,
            });
        }
    }

    private static async Task<Domain.Storage.ArtifactReference> Save(IArtifactService artifacts, string csv, string name, CancellationToken ct)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await artifacts.SaveAsync(stream, $"competitions/demo/{name}", "text/csv", KocDataClassification.Internal, ct);
    }

    private static string Slug(string title)
    {
        var chars = title.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars).Trim('-');
    }

    // ------------------------------------------------------------------ generators

    // Titanic: the real passenger manifest, not an imitation of it. The file is vendored byte-for-byte
    // (Competitions/Data/README.md has the source and checksum) and embedded, so this needs no network
    // and no files on disk. Here we only project the columns the challenge uses and cut the manifest in
    // two — the passengers themselves, their ages and their fates are the historical record.
    //
    // Nothing is cleaned on the way through: 177 of the 891 ages are blank and two passengers have no
    // port of embarkation, and those gaps are the point. A pipeline that does not deal with them throws
    // away a fifth of the manifest or feeds a model an age of zero.
    private static (string, string, string) BuildTitanic()
    {
        const string header = "pclass,sex,age,sibsp,parch,fare,embarked";

        using var resource = typeof(CompetitionSeeder).Assembly
            .GetManifestResourceStream("Beep.KocAiCommunity.Infrastructure.Competitions.Data.titanic.csv")
            ?? throw new InvalidOperationException("The vendored titanic.csv is missing from the assembly.");
        using var reader = new StreamReader(resource);

        var manifest = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var columns = manifest[0].Trim().Split(',');
        int At(string name) => Array.FindIndex(columns, c => c.Equals(name, StringComparison.OrdinalIgnoreCase));

        var (idAt, survivedAt, pclassAt) = (At("PassengerId"), At("Survived"), At("Pclass"));
        var (sexAt, ageAt, sibspAt) = (At("Sex"), At("Age"), At("SibSp"));
        var (parchAt, fareAt, embarkedAt) = (At("Parch"), At("Fare"), At("Embarked"));

        var training = new StringBuilder($"id,{header},survived\n");
        var evaluation = new StringBuilder($"id,{header}\n");
        var answerKey = new StringBuilder("id,survived\n");

        foreach (var line in manifest.Skip(1))
        {
            var cells = SplitCsvLine(line.TrimEnd('\r'));
            var id = cells[idAt];
            var features = string.Join(',',
                cells[pclassAt], cells[sexAt], cells[ageAt], cells[sibspAt], cells[parchAt], cells[fareAt], cells[embarkedAt]);

            // Split on the passenger number, so the same passengers are held back on every install and
            // both halves keep the manifest's own class and survival mix.
            if (int.Parse(id, CultureInfo.InvariantCulture) % 10 < 7)
            {
                training.Append($"{id},{features},{cells[survivedAt]}\n");
            }
            else
            {
                evaluation.Append($"{id},{features}\n");
                answerKey.Append($"{id},{cells[survivedAt]}\n");
            }
        }

        return (training.ToString(), evaluation.ToString(), answerKey.ToString());
    }

    /// <summary>
    /// Splits one CSV line, honouring double quotes. The manifest needs it: every passenger's name is a
    /// quoted field with a comma in it ("Braund, Mr. Owen Harris"), and a naive split would shift every
    /// column after it.
    /// </summary>
    private static string[] SplitCsvLine(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;

        foreach (var c in line)
        {
            switch (c)
            {
                case '"':
                    quoted = !quoted;
                    break;
                case ',' when !quoted:
                    cells.Add(cell.ToString());
                    cell.Clear();
                    break;
                default:
                    cell.Append(c);
                    break;
            }
        }

        cells.Add(cell.ToString());
        return [.. cells];
    }

    // Well integrity: high annulus pressure + water cut + age ⇒ intervention (true).
    private static (string, string, string) BuildWellIntegrity()
    {
        const string header = "casing_pressure,annulus_pressure,temperature,water_cut,age_years";
        var rnd = new Random(20260102);

        (string, string) Row()
        {
            var casing = Gauss(rnd, 1800, 500);
            var annulus = Math.Max(0, Gauss(rnd, 300, 220));
            var temp = Gauss(rnd, 95, 20);
            var water = Math.Clamp(Gauss(rnd, 45, 28), 0, 98);
            var age = (int)Math.Clamp(Gauss(rnd, 14, 8), 1, 35);
            var score = (annulus / 900.0 * 1.4) + (water / 100.0 * 1.2) + (age / 35.0 * 1.0) + (temp / 140.0 * 0.4) - 1.7 + Gauss(rnd, 0, 0.45);
            var label = score > 0 ? "true" : "false";
            return ($"{F(casing)},{F(annulus)},{F(temp)},{F(water)},{age}", label);
        }

        return Emit(header, "label", 520, 150, Row);
    }

    // ESP predictive maintenance: hot motor + vibration + current + runtime ⇒ failure.
    private static (string, string, string) BuildEsp()
    {
        const string header = "intake_pressure,motor_temp,vibration,current,flow_rate,runtime_hours";
        var rnd = new Random(20260103);

        (string, string) Row()
        {
            var intake = Gauss(rnd, 750, 250);
            var motor = Gauss(rnd, 150, 40);
            var vibration = Math.Max(0.05, Gauss(rnd, 2.4, 1.3));
            var current = Gauss(rnd, 65, 22);
            var flow = Math.Max(50, Gauss(rnd, 1900, 700));
            var runtime = Math.Max(50, Gauss(rnd, 4200, 2200));
            var score = (motor / 260.0 * 1.3) + (vibration / 6.0 * 1.5) + (current / 120.0 * 0.7) + (runtime / 9000.0 * 0.9) - 1.95 + Gauss(rnd, 0, 0.4);
            var failure = score > 0 ? "true" : "false";
            return ($"{F(intake)},{F(motor)},{F(vibration)},{F(current)},{F(flow)},{F(runtime)}", failure);
        }

        return Emit(header, "failure", 560, 160, Row);
    }

    // Daily oil rate: a mostly-linear response with noise (learnable regression target).
    private static (string, string, string) BuildProduction()
    {
        const string header = "choke,tubing_pressure,water_cut,gas_rate,days_online";
        var rnd = new Random(20260104);

        (string, string) Row()
        {
            var choke = (int)Math.Clamp(Gauss(rnd, 32, 14), 8, 64);
            var thp = Gauss(rnd, 1200, 500);
            var water = Math.Clamp(Gauss(rnd, 40, 25), 0, 95);
            var gas = Math.Max(0, Gauss(rnd, 1200, 700));
            var days = (int)Math.Clamp(Gauss(rnd, 900, 700), 20, 3200);
            var oil = Math.Max(0, (12 * choke) + (0.6 * thp) - (8 * water) + (0.15 * gas) - (0.05 * days) + Gauss(rnd, 0, 45));
            return ($"{choke},{F(thp)},{F(water)},{F(gas)},{days}", F(oil));
        }

        return Emit(header, "oil_rate", 560, 160, Row);
    }

    // Production decline over time: oil rate falls as water cut climbs and the well ages, on top of
    // day-to-day operating swings. The eval set is the NEXT 90 days (not a random hold-out), so a fair
    // model must train on the past and forecast the future — hence the Chronological split node. Most of
    // the signal lives in in-range operating features (water cut, choke) so it stays learnable.
    private static (string, string, string) BuildProductionForecast()
    {
        var rnd = new Random(20260106);
        const string header = "date,choke,tubing_pressure,water_cut,gas_rate,days_online";
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        (string Id, string Features, string Label) Day(int d)
        {
            var date = start.AddDays(d).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var daysOnline = 200 + d;
            var choke = (int)Math.Clamp(Gauss(rnd, 32, 6), 16, 52);
            var thp = Gauss(rnd, 1200, 180);
            var water = Math.Clamp(20 + (d * 0.09) + Gauss(rnd, 0, 4), 0, 95);
            var gas = Math.Max(0, Gauss(rnd, 1200, 300));
            var oil = Math.Max(0,
                1650 - (8 * water) - (0.4 * daysOnline) + (10 * (choke - 32)) + (0.2 * (thp - 1200)) + Gauss(rnd, 0, 40));
            return ($"d{d:000}", $"{date},{choke},{F(thp)},{F(water)},{F(gas)},{daysOnline}", F(oil));
        }

        const int trainDays = 300;
        const int evalDays = 90;

        var training = new StringBuilder($"id,{header},oil_rate\n");
        for (var d = 0; d < trainDays; d++)
        {
            var (id, features, label) = Day(d);
            training.Append($"{id},{features},{label}\n");
        }

        var evaluation = new StringBuilder($"id,{header}\n");
        var answerKey = new StringBuilder("id,oil_rate\n");
        for (var d = trainDays; d < trainDays + evalDays; d++)
        {
            var (id, features, label) = Day(d);
            evaluation.Append($"{id},{features}\n");
            answerKey.Append($"{id},{label}\n");
        }

        return (training.ToString(), evaluation.ToString(), answerKey.ToString());
    }

    // ESP anomaly detection: normal readings are driven by one latent load, so all five sensors move
    // together (the data lies near a low-dimensional subspace). An anomaly spikes ONE sensor off its
    // correlated value, breaking the pattern — exactly what RandomizedPCA's reconstruction error catches.
    // Training is normal-only (unsupervised); the label lives only in the evaluation answer key.
    private static (string, string, string) BuildAnomaly()
    {
        var rnd = new Random(20260107);
        const string header = "intake_pressure,motor_temp,vibration,current,flow_rate";

        double[] Draw()
        {
            var f = Gauss(rnd, 0, 1); // latent load the whole pump tracks
            return
            [
                700 + (60 * f) + Gauss(rnd, 0, 6),
                120 + (30 * f) + Gauss(rnd, 0, 3),
                Math.Max(0.1, 2.0 + (0.8 * f) + Gauss(rnd, 0, 0.15)),
                55 + (12 * f) + Gauss(rnd, 0, 1.5),
                1800 + (250 * f) + Gauss(rnd, 0, 25),
            ];
        }

        double[] Anomaly()
        {
            var r = Draw();
            switch (rnd.Next(3))
            {
                case 0: r[2] += 6.5; break; // vibration spike
                case 1: r[1] += 90; break;  // motor overheating
                default: r[3] += 45; break; // current surge
            }

            return r;
        }

        static string Fmt(double[] r) => string.Join(',', r.Select(F));

        var training = new StringBuilder($"id,{header}\n");
        for (var i = 0; i < 500; i++)
        {
            training.Append($"n{i},{Fmt(Draw())}\n");
        }

        var evaluation = new StringBuilder($"id,{header}\n");
        var answerKey = new StringBuilder("id,label\n");
        for (var k = 0; k < 160; k++)
        {
            var anomaly = k % 6 == 0; // ~1 in 6 is abnormal
            evaluation.Append($"e{k},{Fmt(anomaly ? Anomaly() : Draw())}\n");
            answerKey.Append($"e{k},{(anomaly ? 1 : 0)}\n");
        }

        return (training.ToString(), evaluation.ToString(), answerKey.ToString());
    }

    // Facies from logs: pick the rock type first, then draw the log responses typical of it.
    //
    // The spreads are wide enough that the classes genuinely overlap, which is the whole difficulty of
    // log interpretation — a shaly sand reads much like a silty shale, and density alone will not tell
    // limestone from dolomite. An earlier version drew them far enough apart that a first-afternoon
    // pipeline scored a perfect 1.0000, which is not a competition: nobody could place above anybody.
    // Photoelectric factor remains the one clean discriminator for limestone, as it is in the real world.
    private static (string, string, string) BuildFacies()
    {
        const string header = "gamma_ray,resistivity,density,neutron_porosity,photoelectric";
        var rnd = new Random(20260105);

        (string, string) Row()
        {
            var facies = Pick(rnd, ("sandstone", 0.40), ("shale", 0.35), ("limestone", 0.15), ("dolomite", 0.10));
            (double gr, double res, double den, double phi, double pe) = facies switch
            {
                "sandstone" => (Gauss(rnd, 45, 25), Gauss(rnd, 40, 30), Gauss(rnd, 2.38, 0.13), Gauss(rnd, 21, 10), Gauss(rnd, 2.1, 0.6)),
                "shale" => (Gauss(rnd, 102, 30), Gauss(rnd, 12, 10), Gauss(rnd, 2.49, 0.13), Gauss(rnd, 30, 10), Gauss(rnd, 3.2, 0.9)),
                "limestone" => (Gauss(rnd, 28, 16), Gauss(rnd, 190, 130), Gauss(rnd, 2.68, 0.10), Gauss(rnd, 13, 9), Gauss(rnd, 4.8, 0.7)),
                _ => (Gauss(rnd, 35, 18), Gauss(rnd, 150, 110), Gauss(rnd, 2.79, 0.10), Gauss(rnd, 10, 8), Gauss(rnd, 3.2, 0.7)),
            };
            var features = $"{F(Math.Max(5, gr))},{F(Math.Max(0.2, res))},{F(den)},{F(Math.Clamp(phi, 0, 48))},{F(Math.Max(1, pe))}";
            return (features, facies);
        }

        return Emit(header, "facies", 640, 180, Row);
    }

    // Porosity from logs: draw the true pore space and the shale content first, then the log responses
    // they produce. Bulk density falls as porosity rises (the physical relationship a model can find);
    // shale inflates the neutron log, so a model that ignores gamma ray reads shale as porosity.
    private static (string, string, string) BuildPorosity()
    {
        const string header = "gamma_ray,resistivity,density,neutron_porosity,photoelectric";
        var rnd = new Random(20260108);

        (string, string) Row()
        {
            var porosity = Math.Clamp(Gauss(rnd, 17, 7), 2, 34);
            var shale = Math.Clamp(Gauss(rnd, 0.3, 0.22), 0, 0.95);
            var gamma = 18 + (shale * 120) + Gauss(rnd, 0, 6);
            var density = 2.71 - (0.0163 * porosity) - (0.06 * shale) + Gauss(rnd, 0, 0.02);
            var neutron = Math.Clamp(porosity + (14 * shale) + Gauss(rnd, 0, 1.6), 0, 50);
            var resistivity = Math.Max(0.3, (90 * Math.Exp(-3.2 * shale) / Math.Max(1, porosity / 12.0)) + Gauss(rnd, 0, 4));
            var pe = Math.Max(1, 1.9 + (1.4 * shale) + Gauss(rnd, 0, 0.2));
            return ($"{F(gamma)},{F(resistivity)},{F(density)},{F(neutron)},{F(pe)}", F(porosity));
        }

        return Emit(header, "porosity", 620, 170, Row);
    }

    // Rate of penetration: what the driller sets (weight, rotary speed, flow) against what the hole
    // gives back (formation hardness, bit wear, depth). Mostly additive, so it is learnable, but the
    // hardness and wear penalties are large enough that pushing weight on bit alone will not win.
    private static (string, string, string) BuildRop()
    {
        const string header = "weight_on_bit,rotary_rpm,mud_flow_rate,formation_hardness,bit_wear,depth_m";
        var rnd = new Random(20260109);

        (string, string) Row()
        {
            var wob = Math.Clamp(Gauss(rnd, 24, 8), 5, 50);
            var rpm = Math.Clamp(Gauss(rnd, 120, 35), 40, 220);
            var flow = Math.Clamp(Gauss(rnd, 620, 130), 300, 950);
            var hardness = Math.Clamp(Gauss(rnd, 5.2, 1.9), 1, 10);
            var wear = Math.Clamp(Gauss(rnd, 3.1, 1.8), 0, 8);
            var depth = Math.Clamp(Gauss(rnd, 2400, 900), 400, 4800);

            var rop = Math.Max(0.6,
                6 + (0.62 * wob) + (0.085 * rpm) + (0.004 * flow)
                - (1.85 * hardness) - (0.95 * wear) - (0.0011 * depth) + Gauss(rnd, 0, 1.5));

            return ($"{F(wob)},{F(rpm)},{F(flow)},{F(hardness)},{F(wear)},{F(depth)}", F(rop));
        }

        return Emit(header, "rop", 600, 165, Row);
    }

    // Corrosion risk: a continuous damage score cut into three bands. Velocity enters as a threshold
    // rather than a slope — below ~1 m/s the water phase separates and sits on the steel — so a linear
    // model leaves something on the table that a tree finds.
    private static (string, string, string) BuildCorrosionRisk()
    {
        const string header = "wall_loss_pct,water_cut,co2_partial_pressure,h2s_ppm,flow_velocity,temperature,age_years,coating";
        var rnd = new Random(20260110);

        (string, string) Row()
        {
            var wallLoss = Math.Clamp(Gauss(rnd, 12, 8), 0, 45);
            var water = Math.Clamp(Gauss(rnd, 42, 26), 0, 98);
            var co2 = Math.Max(0.05, Gauss(rnd, 1.4, 0.9));
            var h2s = Math.Max(0, Gauss(rnd, 220, 190));
            var velocity = Math.Clamp(Gauss(rnd, 2.4, 1.1), 0.2, 7);
            var temp = Gauss(rnd, 58, 18);
            var age = (int)Math.Clamp(Gauss(rnd, 16, 9), 1, 40);
            var coating = Pick(rnd, ("intact", 0.45), ("degraded", 0.38), ("failed", 0.17));

            var score = (wallLoss / 45.0 * 2.2) + (water / 100.0 * 1.1) + (co2 / 4.0 * 0.9)
                + (h2s / 800.0 * 0.8) + (age / 40.0 * 1.0) + (temp / 110.0 * 0.5)
                + (velocity < 1.0 ? 0.6 : 0)
                + coating switch { "failed" => 1.1, "degraded" => 0.5, _ => 0.0 }
                + Gauss(rnd, 0, 0.35);

            var risk = score > 3.1 ? "high" : score > 2.0 ? "medium" : "low";
            return ($"{F(wallLoss)},{F(water)},{F(co2)},{F(h2s)},{F(velocity)},{F(temp)},{age},{coating}", risk);
        }

        return Emit(header, "risk", 660, 180, Row);
    }

    // Near-miss potential severity: energy source and height carry most of it, PPE and night work add
    // to it, and crew_size is deliberately noise — a column that looks plausible and predicts nothing.
    private static (string, string, string) BuildNearMissSeverity()
    {
        const string header = "energy_source,height_m,load_kg,ppe_compliant,shift,hours_into_shift,crew_size,contractor";
        var rnd = new Random(20260111);

        (string, string) Row()
        {
            var energy = Pick(rnd, ("gravity", 0.30), ("pressure", 0.22), ("electrical", 0.16), ("chemical", 0.14), ("mechanical", 0.18));
            var height = energy == "gravity"
                ? Math.Clamp(Gauss(rnd, 4.5, 3), 0, 22)
                : Math.Clamp(Gauss(rnd, 1.2, 1), 0, 8);
            var load = Math.Max(0, Gauss(rnd, 180, 220));
            var ppe = rnd.NextDouble() < 0.78;
            var shift = Pick(rnd, ("day", 0.62), ("night", 0.38));
            var hours = Math.Clamp(Gauss(rnd, 6, 3), 0, 12);
            var crew = (int)Math.Clamp(Gauss(rnd, 5, 2), 1, 14);
            var contractor = rnd.NextDouble() < 0.45;

            var score = (height / 22.0 * 2.1) + (load / 900.0 * 1.2)
                + (ppe ? 0 : 1.0) + (shift == "night" ? 0.45 : 0)
                + (hours / 12.0 * 0.6) + (contractor ? 0.35 : 0)
                + energy switch { "electrical" => 0.8, "pressure" => 0.7, "chemical" => 0.6, _ => 0.2 }
                + Gauss(rnd, 0, 0.3);

            var severity = score > 2.6 ? "major" : score > 1.6 ? "serious" : "minor";
            return ($"{energy},{F(height)},{F(load)},{Bool(ppe)},{shift},{F(hours)},{crew},{Bool(contractor)}", severity);
        }

        return Emit(header, "severity", 640, 175, Row);
    }

    // Diabetes screening: one latent metabolic burden drives the whole routine panel AND the
    // confirmatory HbA1c. The panel is given; the HbA1c is not — predicting it from cheaper bloods is
    // the task. The threshold is set where roughly a third screen positive, so accuracy means something
    // (at a true diabetes prevalence a model that always says "no" would score ~92%).
    private static (string, string, string) BuildDiabetesScreening()
    {
        const string header = "age,bmi,waist_cm,fasting_glucose,triglycerides,hdl,systolic_bp,family_history,physically_active";
        var rnd = new Random(20260112);

        (string, string) Row()
        {
            var age = (int)Math.Clamp(Gauss(rnd, 42, 12), 20, 75);
            var bmi = Math.Clamp(Gauss(rnd, 28.5, 5), 17, 48);
            var waist = Math.Clamp((bmi * 2.6) + Gauss(rnd, 12, 6), 60, 145);
            var family = rnd.NextDouble() < 0.34;
            var active = rnd.NextDouble() < 0.42;

            var burden = ((bmi - 24) / 10.0 * 1.15) + ((age - 40) / 20.0 * 0.8)
                + (family ? 0.75 : 0) + (active ? -0.55 : 0) + Gauss(rnd, 0, 0.55);

            var glucose = Math.Clamp(92 + (burden * 11) + Gauss(rnd, 0, 6), 70, 190);
            var triglycerides = Math.Clamp(115 + (burden * 42) + Gauss(rnd, 0, 30), 40, 480);
            var hdl = Math.Clamp(56 - (burden * 7) + Gauss(rnd, 0, 6), 20, 95);
            var systolic = Math.Clamp(118 + (burden * 8) + Gauss(rnd, 0, 9), 95, 185);

            // The confirmatory test, deliberately absent from the feature set.
            var hba1c = 5.2 + (burden * 0.62) + Gauss(rnd, 0, 0.22);

            return ($"{age},{F(bmi)},{F(waist)},{F(glucose)},{F(triglycerides)},{F(hdl)},{F(systolic)},{Bool(family)},{Bool(active)}",
                Bool(hba1c >= 5.85));
        }

        return Emit(header, "screen_positive", 640, 180, Row);
    }

    // Analyser QC: every channel of the daily control sample tracks one calibration state, so in-control
    // runs lie near a line through five-dimensional space. A drift pushes ONE channel off it — the same
    // shape as the ESP fault, which is why reconstruction error finds it without ever seeing a label.
    private static (string, string, string) BuildLabQc()
    {
        var rnd = new Random(20260113);
        const string header = "sodium,potassium,chloride,glucose,creatinine,run_temperature";

        double[] Draw()
        {
            var calibration = Gauss(rnd, 0, 1);
            return
            [
                140 + (1.1 * calibration) + Gauss(rnd, 0, 0.35),
                4.2 + (0.09 * calibration) + Gauss(rnd, 0, 0.03),
                102 + (0.95 * calibration) + Gauss(rnd, 0, 0.3),
                5.4 + (0.14 * calibration) + Gauss(rnd, 0, 0.05),
                88 + (2.4 * calibration) + Gauss(rnd, 0, 0.9),
                37.0 + (0.12 * calibration) + Gauss(rnd, 0, 0.05),
            ];
        }

        double[] Drifted()
        {
            var r = Draw();
            switch (rnd.Next(4))
            {
                case 0: r[1] += 0.65; break;  // potassium electrode tiring
                case 1: r[3] -= 0.90; break;  // new glucose reagent lot
                case 2: r[4] += 14.0; break;  // creatinine bias
                default: r[0] -= 4.50; break; // sodium calibration slip
            }

            return r;
        }

        static string Fmt(double[] r) => string.Join(',', r.Select(F));

        var training = new StringBuilder($"id,{header}\n");
        for (var i = 0; i < 500; i++)
        {
            training.Append($"qc{i},{Fmt(Draw())}\n");
        }

        var evaluation = new StringBuilder($"id,{header}\n");
        var answerKey = new StringBuilder("id,label\n");
        for (var k = 0; k < 160; k++)
        {
            var drifted = k % 6 == 0;
            evaluation.Append($"e{k},{Fmt(drifted ? Drifted() : Draw())}\n");
            answerKey.Append($"e{k},{(drifted ? 1 : 0)}\n");
        }

        return (training.ToString(), evaluation.ToString(), answerKey.ToString());
    }

    // Triage: one latent illness severity drives every observation, and the acuity bands are cuts on it.
    // The bands are deliberately unbalanced — resuscitation is ~5% — because that imbalance is the real
    // difficulty of the problem, not an accident of the generator.
    private static (string, string, string) BuildTriage()
    {
        const string header = "age,heart_rate,systolic_bp,respiratory_rate,spo2,temperature,pain_score,arrival_by_ambulance";
        var rnd = new Random(20260114);

        (string, string) Row()
        {
            var age = (int)Math.Clamp(Gauss(rnd, 41, 19), 16, 92);
            var severity = Math.Clamp(Gauss(rnd, 0, 1), -3, 4);

            var hr = Math.Clamp(78 + (severity * 17) + Gauss(rnd, 0, 6), 38, 180);
            var sbp = Math.Clamp(128 - (severity * 13) + Gauss(rnd, 0, 8), 60, 200);
            var rr = Math.Clamp(16 + (severity * 5) + Gauss(rnd, 0, 1.5), 8, 46);
            var spo2 = Math.Clamp(98 - (severity * 3.1) + Gauss(rnd, 0, 0.8), 74, 100);
            var temp = Math.Clamp(36.8 + (severity * 0.45) + Gauss(rnd, 0, 0.25), 34.5, 41);
            var pain = (int)Math.Clamp(3 + (severity * 1.9) + Gauss(rnd, 0, 1.4), 0, 10);
            var ambulance = rnd.NextDouble() < 0.18 + (severity * 0.14);

            var acuity = severity > 1.7 ? "resuscitation"
                : severity > 0.8 ? "emergent"
                : severity > -0.3 ? "urgent"
                : "standard";

            return ($"{age},{F(hr)},{F(sbp)},{F(rr)},{F(spo2)},{F(temp)},{pain},{Bool(ambulance)}", acuity);
        }

        return Emit(header, "acuity", 660, 180, Row);
    }

    // Length of stay: additive in the things a ward round would name, with albumin carrying more than
    // its place on the form suggests. Floored at one day, because nobody stays for less.
    private static (string, string, string) BuildLengthOfStay()
    {
        const string header = "age,admission_type,comorbidity_count,creatinine,albumin,white_cell_count,prior_admissions_12m,surgery";
        var rnd = new Random(20260115);

        (string, string) Row()
        {
            var age = (int)Math.Clamp(Gauss(rnd, 54, 18), 18, 94);
            var admission = Pick(rnd, ("emergency", 0.58), ("elective", 0.42));
            var comorbidity = (int)Math.Clamp(Gauss(rnd, 1.8, 1.4), 0, 8);
            var creatinine = Math.Clamp(Gauss(rnd, 95, 38), 40, 320);
            var albumin = Math.Clamp(Gauss(rnd, 38, 6), 18, 52);
            var wcc = Math.Clamp(Gauss(rnd, 8.6, 3.4), 2, 28);
            var prior = (int)Math.Clamp(Gauss(rnd, 0.8, 1.1), 0, 7);
            var surgery = rnd.NextDouble() < 0.37;

            var los = Math.Max(1,
                1.6 + ((age - 50) / 10.0 * 0.55) + (comorbidity * 0.9)
                + (admission == "emergency" ? 1.7 : 0) + (surgery ? 2.4 : 0)
                + ((creatinine - 95) / 50.0 * 0.8) + ((38 - albumin) / 6.0 * 0.9)
                + ((wcc - 8.6) / 4.0 * 0.5) + (prior * 0.45) + Gauss(rnd, 0, 1.1));

            return ($"{age},{admission},{comorbidity},{F(creatinine)},{F(albumin)},{F(wcc)},{prior},{Bool(surgery)}", F(los));
        }

        return Emit(header, "los_days", 640, 180, Row);
    }

    // Course completion: one latent "will they see it through", driven by what T&CD can actually see on
    // the day of enrolment. Booking well ahead and a history of finishing help; a long course, a heavy
    // quarter and previous no-shows hurt. Online is where completion quietly falls over — which is the
    // finding the challenge exists to surface.
    private static (string, string, string) BuildCourseCompletion()
    {
        const string header = "grade,tenure_years,delivery_mode,course_length,days_booked_ahead,prior_courses,prior_no_shows,workload_index";
        var rnd = new Random(20260118);

        (string, string) Row()
        {
            var grade = (int)Math.Clamp(Gauss(rnd, 7, 2.4), 1, 14);
            var tenure = Math.Clamp(Gauss(rnd, 9, 6.5), 0.2, 34);
            var mode = Pick(rnd, ("classroom", 0.42), ("online", 0.38), ("blended", 0.20));
            var length = Math.Clamp(Gauss(rnd, 3.2, 2.1), 0.5, 12);
            var booked = (int)Math.Clamp(Gauss(rnd, 24, 18), 0, 120);
            var prior = (int)Math.Clamp(Gauss(rnd, 3.4, 2.8), 0, 18);
            var noShows = (int)Math.Clamp(Gauss(rnd, 0.5, 0.9), 0, 6);
            var workload = Math.Clamp(Gauss(rnd, 5, 2), 1, 10);

            var willFinish = 1.15
                + (prior / 18.0 * 1.25)
                + (booked / 120.0 * 0.85)
                - (noShows * 0.62)
                - (length / 12.0 * 1.35)
                - (workload / 10.0 * 1.15)
                + mode switch { "online" => -0.85, "blended" => -0.20, _ => 0.35 }
                + Gauss(rnd, 0, 0.55);

            return ($"{grade},{F(tenure)},{mode},{F(length)},{booked},{prior},{noShows},{F(workload)}",
                Bool(willFinish > 0));
        }

        return Emit(header, "completed", 660, 180, Row);
    }

    // Payroll integrity: a clean run ties every component to one latent month — hours drive base pay,
    // overtime tracks hours, allowances and deductions follow grade. A whole month moving together is a
    // pay award and entirely normal; ONE component breaking step is the error. The same shape as the ESP
    // and lab-QC challenges, which is why it is ranked rather than scored on accuracy.
    //
    // The net is deliberately recomputed from the broken parts, so every bad run still balances. Anyone
    // hoping to find these with arithmetic instead of a model will come away empty.
    private static (string, string, string) BuildPayroll()
    {
        var rnd = new Random(20260119);
        const string header = "hours_worked,base_pay,overtime_pay,allowances,deductions,net_pay";

        double[] Draw()
        {
            var month = Gauss(rnd, 0, 1);
            var hours = 160 + (9 * month) + Gauss(rnd, 0, 2.5);
            var basePay = 940 + (7.5 * month) + Gauss(rnd, 0, 9);
            var overtime = Math.Max(0, (hours - 160) * 8.4) + Gauss(rnd, 0, 6);
            var allowances = 250 + (2.1 * month) + Gauss(rnd, 0, 5);
            var deductions = 118 + (0.9 * month) + Gauss(rnd, 0, 3);
            return [hours, basePay, overtime, allowances, deductions, basePay + overtime + allowances - deductions];
        }

        double[] Broken()
        {
            var r = Draw();
            switch (rnd.Next(4))
            {
                case 0: r[1] *= 2; break;        // paid twice
                case 1: r[2] *= 9.5; break;      // a rate entered an order of magnitude out
                case 2: r[0] += 95; break;       // overtime nobody could have worked
                default: r[4] = 0; break;        // deductions silently dropped
            }

            r[5] = r[1] + r[2] + r[3] - r[4];
            return r;
        }

        static string Fmt(double[] r) => string.Join(',', r.Select(F));

        var training = new StringBuilder($"id,{header}\n");
        for (var i = 0; i < 520; i++)
        {
            training.Append($"pr{i},{Fmt(Draw())}\n");
        }

        var evaluation = new StringBuilder($"id,{header}\n");
        var answerKey = new StringBuilder("id,label\n");
        for (var k = 0; k < 170; k++)
        {
            var broken = k % 7 == 0;
            evaluation.Append($"e{k},{Fmt(broken ? Broken() : Draw())}\n");
            answerKey.Append($"e{k},{(broken ? 1 : 0)}\n");
        }

        return (training.ToString(), evaluation.ToString(), answerKey.ToString());
    }

    // Course demand: seats taken follows the size of the discipline it serves and the renewal wave far
    // more than last year's intake does. Summer empties out — the seasonal term is deliberately large
    // enough that a model ignoring the quarter cannot reach the baseline.
    private static (string, string, string) BuildCourseDemand()
    {
        const string header = "course_family,quarter,discipline_headcount,renewals_due,last_year_seats,campaign,budget_index";
        var rnd = new Random(20260120);

        (string, string) Row()
        {
            var family = Pick(rnd, ("safety", 0.30), ("technical", 0.34), ("digital", 0.22), ("leadership", 0.14));
            var quarter = rnd.Next(1, 5);
            var headcount = (int)Math.Clamp(Gauss(rnd, 340, 160), 40, 1200);
            var renewals = (int)Math.Clamp(Gauss(rnd, 46, 34), 0, 260);
            var lastYear = (int)Math.Clamp(Gauss(rnd, 62, 28), 4, 210);
            var campaign = Pick(rnd, ("yes", 0.28), ("no", 0.72));
            var budget = Math.Clamp(Gauss(rnd, 1.0, 0.22), 0.4, 1.7);

            var seats = (headcount * 0.085)
                + (renewals * 0.62)
                + (lastYear * 0.30)
                + (campaign == "yes" ? 14 : 0)
                + ((budget - 1.0) * 38)
                + (quarter == 3 ? -26 : quarter == 1 ? 9 : 0)      // Q3 is the summer trough
                + family switch { "safety" => 12, "digital" => 6, "leadership" => -8, _ => 0 }
                + Gauss(rnd, 0, 7);

            return ($"{family},{quarter},{headcount},{renewals},{lastYear},{campaign},{F(budget)}",
                F(Math.Max(0, seats)));
        }

        return Emit(header, "seats_taken", 620, 170, Row);
    }

    // Time to competency: mentor load is the dominant term, which is the finding T&CD is meant to walk
    // away with — one mentor carrying eight people costs more months than any amount of prior experience
    // saves. Programme length is recorded in months for most rows and weeks for the rest.
    private static (string, string, string) BuildTimeToCompetency()
    {
        const string header = "prior_experience_years,qualification,on_job_pct,mentor_load,rotation_availability,assessments_passed,programme_length";
        var rnd = new Random(20260121);

        (string, string) Row()
        {
            var experience = Math.Clamp(Gauss(rnd, 3.4, 2.8), 0, 18);
            var qualification = Pick(rnd, ("diploma", 0.34), ("bachelor", 0.50), ("master", 0.16));
            var onJob = Math.Clamp(Gauss(rnd, 62, 16), 15, 95);
            var mentorLoad = (int)Math.Clamp(Gauss(rnd, 4.2, 2.1), 1, 11);
            var rotation = Math.Clamp(Gauss(rnd, 0.62, 0.2), 0.1, 1.0);
            var passed = (int)Math.Clamp(Gauss(rnd, 3.1, 1.9), 0, 9);
            var length = Math.Clamp(Gauss(rnd, 14, 4.5), 5, 30);

            var months = 6.5
                + (length * 0.42)
                + (mentorLoad * 1.05)
                - (experience * 0.55)
                - (passed * 0.72)
                - ((rotation - 0.5) * 6.4)
                - ((onJob - 60) * 0.035)
                + qualification switch { "master" => -1.1, "diploma" => 1.0, _ => 0 }
                + Gauss(rnd, 0, 1.3);

            return ($"{F(experience)},{qualification},{F(onJob)},{mentorLoad},{F(rotation)},{passed},{F(length)}",
                F(Math.Clamp(months, 2, 44)));
        }

        return Emit(header, "months_to_signoff", 600, 165, Row);
    }

    // Next track: a scoring rule per track rather than a single latent, so the classes are genuinely
    // distinguishable but overlap where a real recommendation would be a judgement call.
    private static (string, string, string) BuildNextTrack()
    {
        const string header = "discipline,grade,tracks_completed,avg_quiz_score,role_gap,certificate_due,supervises";
        var rnd = new Random(20260122);

        (string, string) Row()
        {
            var discipline = Pick(rnd, ("operations", 0.30), ("engineering", 0.30), ("support", 0.24), ("hse", 0.16));
            var grade = (int)Math.Clamp(Gauss(rnd, 7, 2.6), 1, 14);
            var completed = (int)Math.Clamp(Gauss(rnd, 1.8, 1.5), 0, 7);
            var quiz = Math.Clamp(Gauss(rnd, 71, 14), 25, 100);
            var gap = Math.Clamp(Gauss(rnd, 2.4, 1.2), 0, 6);
            var certDue = Pick(rnd, ("yes", 0.30), ("no", 0.70));
            var supervises = (int)Math.Clamp(Gauss(rnd, 1.6, 2.4), 0, 14);

            var foundations = 3.4 - (completed * 1.95) - ((quiz - 65) * 0.055);
            var data = -0.35 + (completed * 0.40) + ((quiz - 65) * 0.030) - (supervises * 0.13)
                + (certDue == "yes" ? 0.65 : 0);
            var modelling = -1.55 + (completed * 0.98) + ((quiz - 72) * 0.075)
                + (discipline == "engineering" ? 0.9 : 0) - (supervises * 0.18);
            var leading = -2.30 + (supervises * 0.58) + (grade * 0.14) + (gap * 0.25);

            var scores = new[]
            {
                ("foundations", foundations + Gauss(rnd, 0, 0.20)),
                ("data handling", data + Gauss(rnd, 0, 0.20)),
                ("modelling", modelling + Gauss(rnd, 0, 0.20)),
                ("leading a team", leading + Gauss(rnd, 0, 0.20)),
            };

            var best = scores.OrderByDescending(x => x.Item2).First().Item1;
            return ($"{discipline},{grade},{completed},{F(quiz)},{F(gap)},{certDue},{supervises}", best);
        }

        return Emit(header, "next_track", 950, 220, Row);
    }

    // Course rating: everything here is a booking-system field, on purpose — no instructor appears in
    // the data, so nothing a model learns can be read as a judgement about one. A big class late on a
    // Thursday, running on old material, with a third of the room missing the prerequisite, is the shape
    // of a session that disappoints.
    private static (string, string, string) BuildCourseRating()
    {
        const string header = "class_size,delivery_mode,start_hour,day_of_week,material_age_months,prereq_missing_pct,duration_days";
        var rnd = new Random(20260123);

        (string, string) Row()
        {
            var size = (int)Math.Clamp(Gauss(rnd, 18, 8), 4, 48);
            var mode = Pick(rnd, ("classroom", 0.46), ("online", 0.36), ("blended", 0.18));
            var hour = Pick(rnd, (8, 0.42), (10, 0.22), (13, 0.24), (15, 0.12));
            var day = rnd.Next(1, 6);
            var age = Math.Clamp(Gauss(rnd, 16, 12), 0, 62);
            var missing = Math.Clamp(Gauss(rnd, 18, 14), 0, 75);
            var duration = Math.Clamp(Gauss(rnd, 2.4, 1.4), 0.5, 8);

            var disappoints = -2.35
                + ((size - 18) * 0.075)
                + (age * 0.055)
                + (missing * 0.048)
                + (hour >= 13 ? 0.62 : 0)
                + (day >= 4 ? 0.42 : 0)
                + ((duration - 2.4) * 0.20)
                + mode switch { "online" => 0.58, "blended" => 0.10, _ => 0 }
                + Gauss(rnd, 0, 0.55);

            return ($"{size},{mode},{hour},{day},{F(age)},{F(missing)},{F(duration)}", Bool(disappoints > 0));
        }

        return Emit(header, "rated_poorly", 680, 185, Row);
    }

    // Absence: a site-level count, with no individual anywhere in the file. Shutdown months are the
    // discontinuity — leave is refused and days lost collapse, then rebound the month after.
    private static (string, string, string) BuildAbsence()
    {
        const string header = "team_size,shift_pattern,month,approved_leave_days,shutdown_planned,illness_index,last_month_days_lost";
        var rnd = new Random(20260124);

        (string, string) Row()
        {
            var size = (int)Math.Clamp(Gauss(rnd, 34, 16), 6, 120);
            var shift = Pick(rnd, ("day", 0.44), ("rotating", 0.38), ("night", 0.18));
            var month = rnd.Next(1, 13);
            var approved = Math.Clamp(Gauss(rnd, size * 0.55, size * 0.25), 0, size * 3.0);
            var shutdown = Pick(rnd, ("yes", 0.16), ("no", 0.84));
            var illness = Math.Clamp(Gauss(rnd, 1.0, 0.28), 0.3, 2.1);
            var lastMonth = Math.Clamp(Gauss(rnd, size * 0.42, size * 0.18), 0, size * 2.4);

            var days = (size * 0.30 * illness)
                + (approved * 0.45)
                + (lastMonth * 0.28)
                + (shutdown == "yes" ? -size * 0.22 : 0)
                + (month is 7 or 8 ? size * 0.16 : 0)              // the summer leave season
                + (month is 1 or 12 ? size * 0.10 : 0)             // and the winter illness one
                + shift switch { "night" => size * 0.09, "rotating" => size * 0.05, _ => 0 }
                + Gauss(rnd, 0, size * 0.06);

            return ($"{size},{shift},{month},{F(approved)},{shutdown},{F(illness)},{F(lastMonth)}",
                F(Math.Max(0, days)));
        }

        return Emit(header, "days_lost", 640, 175, Row);
    }

    // Time to hire: approvals and scarcity dominate, applicant count barely registers — which is the
    // uncomfortable finding, because advertising harder is the usual response to a slow requisition.
    private static (string, string, string) BuildTimeToHire()
    {
        const string header = "grade,discipline,contract_type,approval_steps,scarcity_index,applicants,month_opened";
        var rnd = new Random(20260125);

        (string, string) Row()
        {
            var grade = (int)Math.Clamp(Gauss(rnd, 8, 2.8), 1, 15);
            var discipline = Pick(rnd, ("engineering", 0.30), ("operations", 0.26), ("support", 0.24), ("medical", 0.20));
            var contract = Pick(rnd, ("permanent", 0.62), ("contract", 0.38));
            var steps = (int)Math.Clamp(Gauss(rnd, 4.2, 1.6), 1, 9);
            var scarcity = Math.Clamp(Gauss(rnd, 1.0, 0.32), 0.25, 2.2);
            var applicants = (int)Math.Clamp(Gauss(rnd, 42, 28), 1, 220);
            var month = rnd.Next(1, 13);

            var days = 22
                + (steps * 11.5)
                + (scarcity * 34)
                + (grade * 2.1)
                - (applicants * 0.06)                              // real, but far weaker than expected
                + (contract == "permanent" ? 14 : 0)
                + (month is 7 or 8 ? 12 : 0)                       // summer slows every approval chain
                + discipline switch { "medical" => 18, "engineering" => 8, _ => 0 }
                + Gauss(rnd, 0, 9);

            return ($"{grade},{discipline},{contract},{steps},{F(scarcity)},{applicants},{month}",
                F(Math.Clamp(days, 9, 260)));
        }

        return Emit(header, "days_to_fill", 620, 170, Row);
    }

    // Overtime: team-level, and the interaction is the point — vacancies and overlapping leave multiply
    // rather than add, so a model that only weighs them separately lands well short of the ceiling.
    private static (string, string, string) BuildOvertime()
    {
        const string header = "headcount,planned_workload,vacancies,leave_overlap_days,shutdown_in_period,shift_pattern,avg_overtime_last3";
        var rnd = new Random(20260126);

        (string, string) Row()
        {
            var headcount = (int)Math.Clamp(Gauss(rnd, 26, 11), 5, 90);
            var workload = Math.Clamp(Gauss(rnd, 1.0, 0.22), 0.45, 1.8);
            var vacancies = (int)Math.Clamp(Gauss(rnd, 2.1, 1.8), 0, 11);
            var overlap = Math.Clamp(Gauss(rnd, 8, 6), 0, 40);
            var shutdown = Pick(rnd, ("yes", 0.20), ("no", 0.80));
            var shift = Pick(rnd, ("day", 0.42), ("rotating", 0.40), ("night", 0.18));
            var last3 = Math.Clamp(Gauss(rnd, 9.5, 4.5), 0, 34);

            var vacancyRate = vacancies / (double)headcount;
            var overCap = -3.25
                + ((workload - 1.0) * 4.2)
                + (vacancyRate * 12.5)
                + (overlap * 0.055)
                + (vacancyRate * overlap * 0.55)                   // the compounding term
                + (last3 * 0.09)
                + (shutdown == "yes" ? 1.05 : 0)
                + shift switch { "rotating" => 0.35, "night" => 0.20, _ => 0 }
                + Gauss(rnd, 0, 0.6);

            return ($"{headcount},{F(workload)},{vacancies},{F(overlap)},{shutdown},{shift},{F(last3)}",
                Bool(overCap > 0));
        }

        return Emit(header, "over_cap", 700, 190, Row);
    }

    // Grade band: one job-weight score cut into four bands. Ordered classes, so the errors a model makes
    // land on neighbouring bands — which is exactly how a disagreeing evaluation committee behaves too.
    private static (string, string, string) BuildGradeBand()
    {
        const string header = "direct_reports,budget_kd,min_qualification,min_experience_years,technical_depth,safety_critical,offshore_pct";
        var rnd = new Random(20260127);

        (string, string) Row()
        {
            var reports = (int)Math.Clamp(Gauss(rnd, 3.2, 4.0), 0, 26);
            var budget = Math.Clamp(Gauss(rnd, 180, 240), 0, 1800);
            var qualification = Pick(rnd, ("secondary", 0.18), ("diploma", 0.30), ("bachelor", 0.38), ("master", 0.14));
            var experience = Math.Clamp(Gauss(rnd, 7.5, 4.8), 0, 28);
            var depth = Math.Clamp(Gauss(rnd, 5, 2.1), 1, 10);
            var safety = Pick(rnd, ("yes", 0.38), ("no", 0.62));
            var offshore = Math.Clamp(Gauss(rnd, 22, 26), 0, 100);

            var weight = (reports * 0.42)
                + (budget / 1800.0 * 5.2)
                + (experience * 0.30)
                + (depth * 0.52)
                + (safety == "yes" ? 0.75 : 0)
                + (offshore / 100.0 * 0.65)
                + qualification switch { "master" => 1.7, "bachelor" => 1.0, "diploma" => 0.35, _ => 0 }
                + Gauss(rnd, 0, 0.45);

            var band = weight switch
            {
                < 3.6 => "support",
                < 5.8 => "professional",
                < 8.2 => "senior",
                _ => "lead",
            };

            return ($"{reports},{F(budget)},{qualification},{F(experience)},{F(depth)},{safety},{F(offshore)}", band);
        }

        return Emit(header, "grade_band", 720, 195, Row);
    }

    // ------------------------------------------------------------------ mess

    /// <summary>
    /// What a real extract looks like once it has been through a historian, a handheld and three
    /// spreadsheets. Applied to the training and evaluation files only — never to the answer key, which
    /// is ground truth and has to stay exact.
    /// </summary>
    /// <param name="Gaps">Columns where a reading sometimes simply is not there.</param>
    /// <param name="Sentinels">
    /// Columns where a failed instrument writes <c>-999</c> rather than nothing. Worse than a gap,
    /// because it is a number: it survives every check that looks for blanks, and quietly drags a mean
    /// or a scaler off with it.
    /// </param>
    /// <param name="Units">One column logged in two different units, with the unit recorded beside it.</param>
    private sealed record Mess(string[] Gaps, string[] Sentinels = null!, UnitSwitch? Units = null)
    {
        /// <summary>About one reading in twenty-five is missing — enough to matter, not enough to maim.</summary>
        public const double GapRate = 0.04;

        /// <summary>Instrument faults are rarer than gaps, and do more damage per row.</summary>
        public const double SentinelRate = 0.015;

        public const string Sentinel = "-999";
    }

    /// <summary>
    /// A column recorded in two units — the second-commonest way a dataset lies to you, after missing
    /// values. Recoverable on purpose: the unit is in the row, so <c>compute-column</c> or a <c>sql</c>
    /// node can put every value back on one scale. A model given the raw column learns two overlapping
    /// populations and splits the difference.
    /// </summary>
    /// <param name="Column">The value column whose unit varies.</param>
    /// <param name="UnitColumn">The column added beside it, naming the unit for that row.</param>
    /// <param name="Native">The unit the generator produced.</param>
    /// <param name="Other">The unit the rest of the rows are converted into.</param>
    /// <param name="Convert">Native → other.</param>
    /// <param name="Share">Fraction of rows recorded in <paramref name="Other"/>.</param>
    private sealed record UnitSwitch(
        string Column, string UnitColumn, string Native, string Other, Func<double, double> Convert, double Share);

    /// <summary>
    /// Dirties the training and evaluation files of an already-generated dataset, leaving the answer key
    /// untouched. Deterministic: the seed decides which cells go, so every install ships the identical
    /// mess and a leaderboard stays comparable across deployments.
    /// <para>
    /// The id and label columns are never touched. A gap in either would not be a cleaning exercise, it
    /// would be an unscorable row.
    /// </para>
    /// </summary>
    private static (string Training, string Evaluation, string AnswerKey) Dirtied(
        (string Training, string Evaluation, string AnswerKey) data, int seed, string labelColumn, Mess mess)
    {
        // One generator for both files, so the training and evaluation halves are dirtied by the same
        // hand but never identically.
        var rnd = new Random(seed);
        return (Apply(data.Training), Apply(data.Evaluation), data.AnswerKey);

        string Apply(string csv)
        {
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var header = lines[0].Trim().Split(',');

            int[] Indexes(string[]? names) =>
            [
                .. (names ?? [])
                    .Select(n => Array.FindIndex(header, h => h.Equals(n, StringComparison.OrdinalIgnoreCase)))
                    .Where(i => i >= 0 && !header[i].Equals(labelColumn, StringComparison.OrdinalIgnoreCase) && i != 0),
            ];

            var gaps = Indexes(mess.Gaps);
            var sentinels = Indexes(mess.Sentinels);
            var unitAt = mess.Units is null ? -1 : Indexes([mess.Units.Column]).FirstOrDefault(-1);

            var output = new StringBuilder(string.Join(',', header));
            if (unitAt >= 0)
            {
                output.Append(',').Append(mess.Units!.UnitColumn);
            }

            output.Append('\n');

            foreach (var line in lines.Skip(1))
            {
                var cells = line.Trim().Split(',');
                var unit = mess.Units?.Native;

                if (unitAt >= 0 && rnd.NextDouble() < mess.Units!.Share
                    && double.TryParse(cells[unitAt], NumberStyles.Any, CultureInfo.InvariantCulture, out var native))
                {
                    cells[unitAt] = F(mess.Units.Convert(native));
                    unit = mess.Units.Other;
                }

                // Sentinels first: a cell that has already been blanked has no instrument left to fail.
                foreach (var i in sentinels)
                {
                    if (rnd.NextDouble() < Mess.SentinelRate)
                    {
                        cells[i] = Mess.Sentinel;
                    }
                }

                foreach (var i in gaps)
                {
                    if (rnd.NextDouble() < Mess.GapRate)
                    {
                        cells[i] = string.Empty;
                    }
                }

                output.Append(string.Join(',', cells));
                if (unitAt >= 0)
                {
                    output.Append(',').Append(unit);
                }

                output.Append('\n');
            }

            return output.ToString();
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Emits (training with label, evaluation without label, answer key) from a row generator.</summary>
    private static (string, string, string) Emit(string featureHeader, string labelColumn, int trainCount, int evalCount, Func<(string Features, string Label)> row)
    {
        var training = new StringBuilder($"id,{featureHeader},{labelColumn}\n");
        for (var i = 0; i < trainCount; i++)
        {
            var (features, label) = row();
            training.Append($"tr{i},{features},{label}\n");
        }

        var evaluation = new StringBuilder($"id,{featureHeader}\n");
        var answerKey = new StringBuilder($"id,{labelColumn}\n");
        for (var k = 0; k < evalCount; k++)
        {
            var (features, label) = row();
            evaluation.Append($"e{k},{features}\n");
            answerKey.Append($"e{k},{label}\n");
        }

        return (training.ToString(), evaluation.ToString(), answerKey.ToString());
    }

    private static double Gauss(Random r, double mean, double sd)
    {
        var u1 = 1.0 - r.NextDouble();
        var u2 = 1.0 - r.NextDouble();
        return mean + (sd * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }

    private static T Pick<T>(Random r, params (T Value, double Weight)[] options)
    {
        var roll = r.NextDouble() * options.Sum(o => o.Weight);
        var cumulative = 0.0;
        foreach (var (value, weight) in options)
        {
            cumulative += weight;
            if (roll <= cumulative)
            {
                return value;
            }
        }

        return options[^1].Value;
    }

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Lower-case true/false — what the scorers canonicalise and ML.NET reads back as a bool.</summary>
    private static string Bool(bool v) => v ? "true" : "false";
}
