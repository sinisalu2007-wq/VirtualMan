using System.Collections.Generic;

namespace VirtualMan;

/// <summary>
/// The anatomy of the virtual body: every organ, the system it belongs to, how vulnerable it is to
/// oxygen starvation, what it says when it fails, and which other organs it NEEDS to work.
///
/// Needs("X", 0.3) means: 30% of this organ's function comes from X. If X is offline this organ
/// loses 30% of its function; if X is at 50% it loses 15%. Because organs depend on organs that
/// depend on organs, one failure can travel through a whole chain
/// (e.g. Hypothalamus -> Pituitary -> Adrenal Glands -> Kidneys).
///
/// Slow(seconds) makes the dependency act gradually (hormones, bone health) instead of instantly.
/// Fast, whole-body effects (blood flow, oxygen, toxins, acid, temperature, infection) are handled
/// by the physiology hubs in Simulation.Compute.
/// </summary>
internal static class Anatomy
{
    public static List<Organ> Build()
    {
        var list = new List<Organ>();

        Organ Add(string name, string system, string layer, double ischemia, bool vital, string failName, string failMessage)
        {
            var organ = new Organ(name, system, layer, ischemia, vital, failName, failMessage);
            list.Add(organ);
            return organ;
        }

        // ------------------------------------------------------------ CIRCULATORY SYSTEM
        const string circ = "Circulatory System";
        Add("Heart", circ, "Cardiovascular", 0.30, true, "Cardiogenic Shock", "Heart stopped pumping. Complete circulatory collapse.");
        Add("Arteries", circ, "Cardiovascular", 0.05, false, "Vascular Collapse", "Arterial tone lost. Blood pressure cannot be maintained.");
        Add("Veins", circ, "Cardiovascular", 0.05, false, "Venous Insufficiency", "Venous return failing. Heart filling drops.")
            .Needs("Skeletal Muscles", 0.15);                       // leg muscles pump blood back to the heart
        Add("Capillaries", circ, "Cardiovascular", 0.10, false, "Microcirculatory Failure", "Gas and nutrient exchange in the tissues lost.");
        Add("Blood", circ, "Cardiovascular", 0.05, true, "Hemorrhagic Shock", "Blood volume and oxygen-carrying capacity critically low.");

        // ------------------------------------------------------------ RESPIRATORY SYSTEM
        const string resp = "Respiratory System";
        Add("Nose", resp, "Respiratory", 0.05, false, "Nasal Obstruction", "Nasal airway blocked. Airflow reduced and filtering lost.");
        Add("Larynx", resp, "Respiratory", 0.05, true, "Airway Obstruction", "Voice box blocked. Air cannot reach the lungs.");
        Add("Trachea", resp, "Respiratory", 0.05, true, "Tracheal Obstruction", "Windpipe blocked. Air cannot reach the lungs.");
        Add("Bronchi", resp, "Respiratory", 0.10, false, "Bronchial Obstruction", "Lower airways blocked. Ventilation of the lungs reduced.");
        Add("Lungs", resp, "Respiratory", 0.15, true, "Respiratory Failure", "Gas exchange lost. Severe hypoxemia.");
        Add("Diaphragm", resp, "Respiratory", 0.10, false, "Diaphragm Paralysis", "Main breathing muscle offline. Ventilation collapses.")
            .Needs("Spinal Cord", 0.30)                             // phrenic nerve roots leave the neck
            .Needs("Peripheral Nerves", 0.30);

        // ------------------------------------------------------------ DIGESTIVE SYSTEM
        const string dig = "Digestive System";
        Add("Mouth", dig, "Digestive", 0.05, false, "Oral Failure", "Cannot take in food or fluid.");
        Add("Teeth", dig, "Digestive", 0.02, false, "Mastication Failure", "Cannot chew food. Digestion impaired.");
        Add("Tongue", dig, "Digestive", 0.05, false, "Tongue Failure", "Cannot manipulate or taste food.")
            .Needs("Peripheral Nerves", 0.30);
        Add("Salivary Glands", dig, "Digestive", 0.05, false, "Salivary Failure", "No saliva. Swallowing and starch digestion impaired.");
        Add("Esophagus", dig, "Digestive", 0.05, false, "Esophageal Obstruction", "Food cannot reach the stomach.")
            .Needs("Brainstem", 0.20);                              // swallowing reflex
        Add("Stomach", dig, "Digestive", 0.10, false, "Gastric Failure", "Stomach not digesting. Nutrient uptake reduced.")
            .Needs("Peripheral Nerves", 0.10);                      // vagus nerve
        Add("Small Intestine", dig, "Digestive", 0.12, false, "Intestinal Failure", "Nutrient absorption lost.")
            .Needs("Pancreas", 0.25)                                // digestive enzymes
            .Needs("Gallbladder", 0.15);                            // bile for fat absorption
        Add("Large Intestine", dig, "Digestive", 0.08, false, "Colonic Failure", "Water absorption and waste handling lost.");
        Add("Anus", dig, "Digestive", 0.03, false, "Bowel Obstruction", "Waste cannot be expelled. Toxin production rises.")
            .Needs("Spinal Cord", 0.20);                            // sphincter control
        Add("Liver", dig, "Digestive", 0.30, false, "Hepatic Failure", "Detoxification offline. Toxins accumulating.");
        Add("Gallbladder", dig, "Digestive", 0.05, false, "Biliary Failure", "Bile storage and drainage lost. Fat digestion impaired.")
            .Needs("Liver", 0.30);                                  // liver makes the bile it stores
        Add("Pancreas", dig, "Digestive", 0.15, false, "Pancreatic Failure", "Digestive enzymes and insulin lost. Energy supply falling.");

        // ------------------------------------------------------------ EXCRETORY (URINARY) SYSTEM
        const string exc = "Excretory System";
        Add("Kidneys", exc, "Excretory", 0.35, false, "Renal Failure", "Filtration ceased. Toxins accumulating.")
            .Needs("Ureters", 0.35)                                 // blocked drainage damages the kidney
            .Needs("Urethra", 0.30)
            .Needs("Bladder", 0.15)
            .Needs("Adrenal Glands", 0.10);                         // aldosterone controls salt and water
        Add("Ureters", exc, "Excretory", 0.05, false, "Ureteral Obstruction", "Urine cannot drain from the kidneys.");
        Add("Bladder", exc, "Excretory", 0.05, false, "Urinary Retention", "Bladder cannot store or empty urine.")
            .Needs("Spinal Cord", 0.30)                             // micturition reflex
            .Needs("Peripheral Nerves", 0.10);
        Add("Urethra", exc, "Excretory", 0.03, false, "Urethral Obstruction", "Outflow blocked. Pressure builds back toward the kidneys.");

        // ------------------------------------------------------------ NERVOUS SYSTEM
        const string nerv = "Nervous System";
        Add("Cerebrum", nerv, "Nervous", 0.60, true, "Cerebral Failure", "Higher brain function lost. Unconsciousness.")
            .Needs("Skull", 0.10);
        Add("Cerebellum", nerv, "Nervous", 0.35, false, "Cerebellar Failure", "Coordination and balance control lost.")
            .Needs("Skull", 0.10);
        Add("Brainstem", nerv, "Nervous", 1.00, true, "Central Brain Death", "Brainstem core offline.")
            .Needs("Skull", 0.10);
        Add("Spinal Cord", nerv, "Nervous", 0.30, false, "Spinal Cord Injury", "Signals between brain and body cut.")
            .Needs("Spine", 0.25);
        Add("Peripheral Nerves", nerv, "Nervous", 0.10, false, "Peripheral Neuropathy", "Nerve signals to limbs and organs weak.")
            .Needs("Spinal Cord", 0.50);

        // ------------------------------------------------------------ SENSORY SYSTEM
        const string sens = "Sensory System";
        Add("Eyes", sens, "Sensory", 0.10, false, "Vision Loss", "Sight lost. Walking becomes cautious.");
        Add("Ear", sens, "Sensory", 0.20, false, "Vestibular Failure", "Balance sensing offline.");

        // ------------------------------------------------------------ ENDOCRINE SYSTEM
        const string endo = "Endocrine System";
        Add("Hypothalamus", endo, "Endocrine", 0.40, false, "Hypothalamic Failure", "Temperature, thirst and hormone control lost.");
        Add("Pituitary", endo, "Endocrine", 0.20, false, "Pituitary Failure", "Master hormone signals lost (TSH, ACTH, sex hormones).")
            .Needs("Hypothalamus", 0.90).Slow(20);
        Add("Thyroid", endo, "Endocrine", 0.10, false, "Thyroid Failure", "Metabolic-rate hormone lost. The body slows down.")
            .Needs("Pituitary", 0.60).Slow(40);
        Add("Adrenal Glands", endo, "Endocrine", 0.20, false, "Adrenal Crisis", "Cortisol and aldosterone lost. Blood pressure and glucose control fail.")
            .Needs("Pituitary", 0.60).Slow(40);

        // ------------------------------------------------------------ IMMUNE & LYMPHATIC SYSTEM
        const string imm = "Immune & Lymphatic System";
        Add("Bone Marrow", imm, "Immune", 0.15, false, "Marrow Failure", "Blood cell production stopped. Anemia and immune loss follow.")
            .Needs("Pelvis", 0.15).Needs("Long Bones", 0.15).Slow(60);   // marrow lives inside the bones
        Add("Thymus", imm, "Immune", 0.05, false, "Thymic Failure", "T-cell maturation lost. Immunity weakens.");
        Add("Spleen", imm, "Immune", 0.10, false, "Splenic Failure", "Blood filtering and immune surveillance lost.");
        Add("Lymph Nodes", imm, "Immune", 0.05, false, "Lymphatic Failure", "Lymph drainage and immune filtering lost.");

        // ------------------------------------------------------------ SKELETAL SYSTEM
        const string skel = "Skeletal System";
        Add("Skull", skel, "Skeletal", 0.02, false, "Skull Fracture", "Protective shell around the brain lost.")
            .Needs("Kidneys", 0.08).Needs("Gonads", 0.05).Slow(180);     // bone health: vitamin D (kidney), sex hormones
        Add("Spine", skel, "Skeletal", 0.02, false, "Spinal Column Failure", "Vertebral column unstable. Spinal cord at risk.")
            .Needs("Kidneys", 0.08).Needs("Gonads", 0.05).Slow(180);
        Add("Rib Cage", skel, "Skeletal", 0.02, false, "Rib Cage Failure", "Chest wall unstable. Breathing mechanics impaired.")
            .Needs("Kidneys", 0.08).Needs("Gonads", 0.05).Slow(180);
        Add("Pelvis", skel, "Skeletal", 0.02, false, "Pelvic Fracture", "Pelvic ring unstable. Walking impaired; marrow at risk.")
            .Needs("Kidneys", 0.08).Needs("Gonads", 0.05).Slow(180);
        Add("Long Bones", skel, "Skeletal", 0.02, false, "Long Bone Fracture", "Limb bones cannot bear load.")
            .Needs("Kidneys", 0.08).Needs("Gonads", 0.05).Slow(180);

        // ------------------------------------------------------------ MUSCULAR SYSTEM
        Add("Skeletal Muscles", "Muscular System", "Muscular", 0.10, false, "Muscle Failure", "Muscle power lost. Movement, body heat and venous pumping fail.")
            .Needs("Peripheral Nerves", 0.35).Needs("Spinal Cord", 0.15)
            .Needs("Thyroid", 0.10).Needs("Gonads", 0.05).Slow(40);

        // ------------------------------------------------------------ INTEGUMENTARY SYSTEM
        Add("Skin", "Integumentary System", "Skin", 0.05, false, "Skin Barrier Failure", "Large skin loss. Fluid and heat escape; infection enters.");

        // ------------------------------------------------------------ REPRODUCTIVE SYSTEM
        const string repro = "Reproductive System";
        Add("Gonads", repro, "Reproductive", 0.05, false, "Gonadal Failure", "Sex hormones lost. Slow loss of bone and muscle strength.")
            .Needs("Pituitary", 0.70).Slow(60);
        Add("Reproductive Tract", repro, "Reproductive", 0.03, false, "Reproductive Tract Failure", "Reproductive function lost.")
            .Needs("Gonads", 0.50).Slow(60);

        // ------------------------------------------------------------ LOCOMOTOR SYSTEM
        Add("Legs", "Locomotor System", "Locomotor", 0.10, false, "Locomotor Failure", "Legs cannot bear weight or walk.")
            .Needs("Skeletal Muscles", 0.35).Needs("Long Bones", 0.30)
            .Needs("Peripheral Nerves", 0.15).Needs("Pelvis", 0.10);

        return list;
    }
}
