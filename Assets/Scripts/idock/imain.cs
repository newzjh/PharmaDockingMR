using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;

namespace idock
{




    public class iConfig
    {
        public Dictionary<string, string> properites = new Dictionary<string, string>();

        public iConfig(string path)
        {
            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    string[] cols = line.Split('=');
                    if (cols != null && cols.Length == 2)
                    {
                        for (int i = 0; i < cols.Length; i++)
                        {
                            cols[i] = cols[i].Trim();
                        }
                        properites[cols[0]] = cols[1];
                    }
                }
            }
        }

        public string GetValue(string key)
        {
            if (properites.ContainsKey(key))
                return properites[key];
            else
                return "";
        }
    }

    public class imain
    {

        public delegate void trainHandler(int mtry);
        public delegate void precalculateHandler(int t0, int t1);
        public delegate void populateHandler(ref List<int> xs, int z, scoring_function sf);
        public delegate void monte_carloHandler(ref List<result> results, int seed, ref scoring_function sf, ref receptor rec);

        private static void assert(bool b)
        {
        }

        public const int seed = 148615245;
        public const int num_trees = 10;
        public const int num_tasks = 3;
        public const int max_conformations = 9;
        public const float granularity = 0.5f;
        public const int num_threads = 8;

        public static int processByConfig(string cfgpath)
        {
            string receptor_path = "", ligand_path = "", out_path = "";
            Vec3 center = Vec3.zero, size = Vec3.zero;


            string cfgfolder = Path.GetDirectoryName(cfgpath);
            iConfig cfg = new iConfig(cfgpath);
            receptor_path = cfgfolder + "/" + cfg.GetValue("receptor");
            ligand_path = cfgfolder + "/" + cfg.GetValue("ligand");
            receptor_path = Path.GetFullPath(receptor_path);
            ligand_path = Path.GetFullPath(ligand_path);
            float.TryParse(cfg.GetValue("center_x"), out center.x);
            float.TryParse(cfg.GetValue("center_y"), out center.y);
            float.TryParse(cfg.GetValue("center_z"), out center.z);
            float.TryParse(cfg.GetValue("size_x"), out size.x);
            float.TryParse(cfg.GetValue("size_y"), out size.y);
            float.TryParse(cfg.GetValue("size_z"), out size.z);

            // Process program options.
            try
            {
                // Initialize the default values of optional arguments.
                //string default_out_path = ".";
                //int default_seed = 1;//chrono::duration_cast<chrono::seconds>(chrono::system_clock::now().time_since_epoch()).count();
                //int default_num_threads = 3;//boost::thread::hardware_concurrency();
                //int default_num_trees = 500;
                //int default_num_tasks = 64;
                //int default_max_conformations = 9;
                //float default_granularity = 0.125f;

                // Set up options description.
                //using namespace boost::program_options;
                //options_description input_options("input (required)");
                //input_options.add_options()
                //    ("receptor", value<path>(&receptor_path)->required(), "receptor in PDBQT format")
                //    ("ligand", value<path>(&ligand_path)->required(), "ligand or folder of ligands in PDBQT format")
                //    ("center_x", value<double>(&center[0])->required(), "x coordinate of the search space center")
                //    ("center_y", value<double>(&center[1])->required(), "y coordinate of the search space center")
                //    ("center_z", value<double>(&center[2])->required(), "z coordinate of the search space center")
                //    ("size_x", value<double>(&size[0])->required(), "size in the x dimension in Angstrom")
                //    ("size_y", value<double>(&size[1])->required(), "size in the y dimension in Angstrom")
                //    ("size_z", value<double>(&size[2])->required(), "size in the z dimension in Angstrom")
                ;
                //options_description output_options("output (optional)");
                //output_options.add_options()
                //    ("out", value<path>(&out_path)->default_value(default_out_path), "folder of predicted conformations in PDBQT format")
                ;
                //options_description miscellaneous_options("options (optional)");
                //miscellaneous_options.add_options()
                //    ("seed", value<int>(&seed)->default_value(default_seed), "explicit non-negative random seed")
                //    ("threads", value<int>(&num_threads)->default_value(default_num_threads), "number of worker threads to use")
                //    ("trees", value<int>(&num_trees)->default_value(default_num_trees), "number of decision trees in random forest")
                //    ("tasks", value<int>(&num_tasks)->default_value(default_num_tasks), "number of Monte Carlo tasks for global search")
                //    ("conformations", value<int>(&max_conformations)->default_value(default_max_conformations), "maximum number of binding conformations to write")
                //    ("granularity", value<double>(&granularity)->default_value(default_granularity), "density of probe atoms of grid maps")
                //    ("score_only", bool_switch(&score_only), "scoring without docking")
                //    ("help", "this help information")
                //    ("version", "version information")
                //    ("config", value<path>(), "configuration file to load options from")
                ;
                //options_description all_options;
                //all_options.add(input_options).add(output_options).add(miscellaneous_options);

                // Parse command line arguments.
                //variables_map vm;
                //store(parse_command_line(argc, argv, all_options), vm);

                // Validate receptor_path.
                if (!File.Exists(receptor_path))
                {
                    //cerr << "Option receptor " << receptor_path << " does not exist" << endl;
                    return 1;
                }

                // Validate ligand_path.
                if (!Directory.Exists(ligand_path))
                {
                    //cerr << "Option ligand " << ligand_path << " does not exist" << endl;
                    return 1;
                }

            }
            catch (Exception e)
            {
                //cerr << e.what() << endl;
                return 1;
            }

            // Parse the receptor.
            //cout << "Parsing the receptor " << receptor_path << endl;
            receptor rec = new receptor(receptor_path, center, size, granularity);



            // Enumerate and sort input ligands.
            //cout << "Enumerating input ligands in " << ligand_path << endl;
            List<string> input_ligand_paths = new List<string>();
            input_ligand_paths.AddRange(Directory.GetFiles(ligand_path, "*.pdbqt"));
            //if (is_regular_file(ligand_path))
            //{
            //    input_ligand_paths.push_back(ligand_path);
            //}
            //else
            //{
            //    for (directory_iterator dir_iter(ligand_path), end_dir_iter; dir_iter != end_dir_iter; ++dir_iter)
            //    {
            //        // Filter files with .pdbqt and .PDBQT extensions.
            //         path input_ligand_path = dir_iter->path();
            //         auto ext = input_ligand_path.extension();
            //        if (ext != ".pdbqt" && ext != ".PDBQT") continue;
            //        input_ligand_paths.push_back(input_ligand_path);
            //    }
            //}
            int num_input_ligands = input_ligand_paths.Count;
            //cout << "Sorting " << num_input_ligands << " input ligands in alphabetical order" << endl;
            //input_ligand_paths.Sort();
            //sort(input_ligand_paths.begin(), input_ligand_paths.end());

            // Initialize a Mersenne Twister random number generator.
            //cout << "Seeding a random number generator with " << seed << endl;
            mt19937_64 rng = new mt19937_64(seed);

            // Initialize an io service pool and create worker threads for later use.
            //cout << "Creating an io service pool of " << num_threads << " worker threads" << endl;
            //io_service_pool io(num_threads);
            AsynPool<int> cnt = new AsynPool<int>();

            // Precalculate the scoring function in parallel.
            //cout << "Calculating a scoring function of " << scoring_function::n << " atom types" << endl;
            scoring_function sf = new scoring_function();
            cnt.init((scoring_function.n + 1) * scoring_function.n >> 1);
            for (int t1 = 0; t1 < scoring_function.n; ++t1)
            {
                for (int t0 = 0; t0 <= t1; ++t0)
                {
                    //io.post([&, t0, t1]()
                    //{
                    //    sf.precalculate(t0, t1);
                    //    cnt.increment();
                    //});
                    precalculateHandler handler = sf.precalculate;
                    //IAsyncResult ar = handler.BeginInvoke(t0, t1, null, sf);
                    ThreadTask ar = new ThreadTask(handler, new object[] { t0, t1 });
                    cnt.post(handler, ar);
                }
            }
            cnt.wait();
            sf.clear();

            // Train RF-Score on the fly.
            //cout << "Training a random forest of " << num_trees << " trees with " << tree::nv << " variables and " << tree::ns << " samples" << endl;
            forest f = new forest(num_trees, seed);
            cnt.init(num_trees);
            //for (int i = 0; i < num_trees; ++i)
            //{
            //    f[i].train(4, f.u01_s);
            //    cnt.increment();
            //}
            Dictionary<Delegate, IAsyncResult> ars = new Dictionary<Delegate, IAsyncResult>();
            for (int i = 0; i < num_trees; ++i)
            {
                trainHandler handler = f[i].train;
                //IAsyncResult ar = handler.BeginInvoke(4, f.u01_s, null, f[i]);
                ThreadTask ar = new ThreadTask(handler, new object[] { 4 });
                cnt.post(handler, ar);
            }
            cnt.wait();
            f.clear();

            // Output headers to the standard output and the log file.
            //cout << "Creating grid maps of " << granularity << " A and running " << num_tasks << " Monte Carlo searches per ligand" << endl
            //<< "   Index             Ligand   nConfs   idock score (kcal/mol)   RF-Score (pKd)" << endl << setprecision(2);
            //cout.setf(ios::fixed, ios::floatfield);
            //boost::filesystem::ofstream log(out_path / "log.csv");
            //log.setf(ios::fixed, ios::floatfield);
            //log << "Ligand,nConfs,idock score (kcal/mol),RF-Score (pKd)" << endl << setprecision(2);

            // Start to dock each input ligand.
            foreach (string input_ligand_path in input_ligand_paths)
            {
                // Reserve storage for result containers.
                List<result>[] result_containers = new List<result>[num_tasks];
                for (int i = 0; i < num_tasks; i++)
                {
                    // 20 Maximum number of results obtained from a single Monte Carlo task.
                    result_containers[i] = new List<result>(20);
                }
                List<result> finalresults = new List<result>(max_conformations);

                // Output the ligand file stem.
                string stem = input_ligand_path;//.stem().string();
                //cout << setw(8) << ++index << "   " << setw(16) << stem << "   " << flush;

                // Check if the current ligand has already been docked.
                int num_confs = 0;
                float id_score = 0;
                float rf_score = 0;
                //path output_ligand_path = out_path / input_ligand_path.filename();
                string output_ligand_path = input_ligand_path + ".ret";

                // Parse the ligand.
                Vec3 origin = Vec3.zero; ;
                ligand lig = new ligand(input_ligand_path, ref origin);

                // Find atom types that are present in the current ligand but not present in the grid maps.
                List<int> xs = new List<int>();
                for (int t = 0; t < scoring_function.n; ++t)
                {
                    if (lig.xs[t] && rec.maps[t].Count <= 0)
                    {
                        //rec.maps[t].resize(rec.num_probes_product);
                        rec.maps[t] = new List<float>(rec.num_probes_product);
                        for (int k = 0; k < rec.num_probes_product; k++)
                            rec.maps[t].Add(0.0f);
                        xs.Add(t);
                    }
                }

                // Create grid maps on the fly if necessary.
                if (xs.Count > 0)
                {
                    // Precalculate p_offset.
                    rec.precalculate(ref xs);

                    // Populate the grid map task container.
                    cnt.init(rec.num_probes[2]);
                    for (int z = 0; z < rec.num_probes[2]; ++z)
                    {
                        //io.post([&, z]()
                        //{
                        //    rec.populate(xs, z, sf);
                        //    cnt.increment();
                        //});
                        populateHandler handler = rec.populate;
                        //IAsyncResult ar = handler.BeginInvoke(ref xs, z, sf, null, rec);
                        ThreadTask ar = new ThreadTask(handler, new object[] {xs,z,sf});
                        cnt.post(handler, ar);
                    }
                    cnt.wait();
                }


                // Run the Monte Carlo tasks.
                cnt.init(num_tasks);
                for (int i = 0; i < num_tasks; ++i)
                {
                    assert(result_containers[i].Count == 0);
                    int s = rng.seed;
                    //io.post([&, i, s]()
                    //{
                    //    lig.monte_carlo(result_containers[i], s, sf, rec);
                    //    cnt.increment();
                    //});
                    monte_carloHandler handler = lig.monte_carlo;
                    //IAsyncResult ar = handler.BeginInvoke(ref result_containers[i], s, ref sf, ref rec, null, lig);
                    ThreadTask ar = new ThreadTask(handler, new object[] { result_containers[i], s, sf, rec });
                    cnt.post(handler, ar);
                    //lig.monte_carlo(ref result_containers[i], s, ref sf, ref rec);

                }
                cnt.wait();

                // Merge results from all tasks into one single result container.
                assert(finalresults.Count==0);
                float required_square_error = 4.0f * lig.num_heavy_atoms; // Ligands with RMSD < 2.0 will be clustered into the same cluster.
                foreach (var result_container in result_containers)
                {
                    foreach (result r in result_container)
                    {
                        result.push(finalresults, r, required_square_error);
                    }
                    result_container.Clear();
                }
                finalresults.Sort(new ResultComparer());

                // If conformations are found, output them.
                num_confs = finalresults.Count;
                if (num_confs > 0)
                {
                    // Adjust free energy relative to the best conformation and flexibility.
                    result best_result = finalresults[0];
                    float best_result_intra_e = best_result.e - best_result.f;
                    //foreach (result r in results)
                    for (int i = 0; i < finalresults.Count; i++)
                    {
                        result r = finalresults[i];
                        r.e_nd = (r.e - best_result_intra_e) * lig.flexibility_penalty_factor;
                        r.rf = lig.calculate_rf_score(ref r, ref rec, ref f);
                    }
                    id_score = best_result.e_nd;
                    rf_score = best_result.rf;


                    // Write models to file.
                    List<string> outputlines = new List<string>();
                    lig.write_models(ref outputlines, ref finalresults, -1, ref rec);
                    File.WriteAllLines(output_ligand_path, outputlines.ToArray());

                    // Clear the results of the current ligand.
                    finalresults.Clear();
                }



                // If output file or conformations are found, output the idock score and RF-Score.
                //cout << setw(6) << num_confs;
                //log << stem << ',' << num_confs;
                if (num_confs > 0)
                {
                    //cout << "   " << setw(22) << id_score << "   " << setw(14) << rf_score;
                    //log << ',' << id_score << ',' << rf_score;
                }
                //cout << endl;
                //log << '\n';

                // Output to the log file in csv format. The log file can be sorted using: head -1 log.csv && tail -n +2 log.csv | awk -F, '{ printf "%s,%s\n", $2||0, $0 }' | sort -t, -k1nr -k3n | cut -d, -f2-
            }

            // Wait until the io service pool has finished all its tasks.
            //io.wait();

            return 1;
        }

        public static string processByMatch(string ligand_path, string receptor_path,
            float centerx, float centery, float centerz, float sizex, float sizey, float sizez)
        {
            Vec3 center = new Vec3(centerx,centery,centerz);
            Vec3 size = new Vec3(sizex,sizey,sizez);


            //string cfgfolder = Path.GetDirectoryName(cfgpath);
            //iConfig cfg = new iConfig(cfgpath);
            //receptor_path = cfgfolder + "/" + cfg.GetValue("receptor");
            //ligand_path = cfgfolder + "/" + cfg.GetValue("ligand");
            //receptor_path = Path.GetFullPath(receptor_path);
            //ligand_path = Path.GetFullPath(ligand_path);
            //float.TryParse(cfg.GetValue("center_x"), out center.x);
            //float.TryParse(cfg.GetValue("center_y"), out center.y);
            //float.TryParse(cfg.GetValue("center_z"), out center.z);
            //float.TryParse(cfg.GetValue("size_x"), out size.x);
            //float.TryParse(cfg.GetValue("size_y"), out size.y);
            //float.TryParse(cfg.GetValue("size_z"), out size.z);

            // Validate ligand path.
            if (!File.Exists(ligand_path))
            {
                return "";
            }

            if (!File.Exists(receptor_path))
            {
                return "";
            }

            // Parse the receptor.
            //cout << "Parsing the receptor " << receptor_path << endl;
            receptor rec = new receptor(receptor_path, center, size, granularity);
            //rec.calcBound();

            // Initialize a Mersenne Twister random number generator.
            //cout << "Seeding a random number generator with " << seed << endl;
            mt19937_64 rng = new mt19937_64(seed);

            // Initialize an io service pool and create worker threads for later use.
            //cout << "Creating an io service pool of " << num_threads << " worker threads" << endl;
            //io_service_pool io(num_threads);
            AsynPool<int> cnt = new AsynPool<int>();

            // Precalculate the scoring function in parallel.
            //cout << "Calculating a scoring function of " << scoring_function::n << " atom types" << endl;
            scoring_function sf = new scoring_function();
            cnt.init((scoring_function.n + 1) * scoring_function.n >> 1);
            for (int t1 = 0; t1 < scoring_function.n; ++t1)
            {
                for (int t0 = 0; t0 <= t1; ++t0)
                {
                    //sf.precalculate(t0, t1);
                    //cnt.increment();
                    precalculateHandler handler = sf.precalculate;
                    //IAsyncResult ar = handler.BeginInvoke(t0, t1, null, sf);
                    ThreadTask ar = new ThreadTask(handler, new object[] { t0, t1 });
                    cnt.post(handler, ar);
                }
            }
            cnt.wait();
            sf.clear();

            // Train RF-Score on the fly.
            //cout << "Training a random forest of " << num_trees << " trees with " << tree::nv << " variables and " << tree::ns << " samples" << endl;
            forest f = new forest(num_trees, seed);
            cnt.init(num_trees);
            for (int i = 0; i < num_trees; ++i)
            {
                //f[i].train(4, f.u01_s);
                //cnt.increment();
                trainHandler handler = f[i].train;
                //IAsyncResult ar = handler.BeginInvoke(4, f.u01_s, null, f[i]);
                ThreadTask ar = new ThreadTask(handler, new object[] { 4 });
                cnt.post(handler, ar);
            }
            cnt.wait();
            f.clear();


            // Reserve storage for result containers.
            List<result>[] result_containers = new List<result>[num_tasks];
            for (int i = 0; i < num_tasks; i++)
            {
                // 20 Maximum number of results obtained from a single Monte Carlo task.
                result_containers[i] = new List<result>(20);
            }
            List<result> finalresults = new List<result>(max_conformations);

            // Check if the current ligand has already been docked.
            int num_confs = 0;
            float id_score = 0;
            float rf_score = 0;
            string output_ligand_path = Path.GetDirectoryName(ligand_path)+"/"+
                Path.GetFileNameWithoutExtension(receptor_path)+"&"+ Path.GetFileNameWithoutExtension(ligand_path) + ".pdb";

            // Parse the ligand.
            Vec3 origin = Vec3.zero; ;
            ligand lig = new ligand(ligand_path, ref origin);

            // Find atom types that are present in the current ligand but not present in the grid maps.
            List<int> xs = new List<int>();
            for (int t = 0; t < scoring_function.n; ++t)
            {
                if (lig.xs[t] && rec.maps[t].Count <= 0)
                {
                    //rec.maps[t].resize(rec.num_probes_product);
                    rec.maps[t] = new List<float>(rec.num_probes_product);
                    for (int k = 0; k < rec.num_probes_product; k++)
                        rec.maps[t].Add(0.0f);
                    xs.Add(t);
                }
            }

            // Create grid maps on the fly if necessary.
            if (xs.Count > 0)
            {
                // Precalculate p_offset.
                rec.precalculate(ref xs);

                // Populate the grid map task container.
                cnt.init(rec.num_probes[2]);
                for (int z = 0; z < rec.num_probes[2]; ++z)
                {
                    //rec.populate(ref xs, z, sf);
                    //cnt.increment();
                    populateHandler handler = rec.populate;
                    //IAsyncResult ar = handler.BeginInvoke(ref xs, z, sf, null, rec);
                    ThreadTask ar = new ThreadTask(handler, new object[] { xs, z, sf });
                    cnt.post(handler, ar);
                }
                cnt.wait();
            }

            // Run the Monte Carlo tasks.
            cnt.init(num_tasks);
            for (int i = 0; i < num_tasks; ++i)
            {
                assert(result_containers[i].Count == 0);
                int s = rng.seed;
                //List<result> result_container = result_containers[i];
                monte_carloHandler handler = lig.monte_carlo;
                //IAsyncResult ar = handler.BeginInvoke(ref result_containers[i], s, ref sf, ref rec, null, lig);
                ThreadTask ar = new ThreadTask(handler, new object[] { result_containers[i], s, sf, rec });
                cnt.post(handler, ar);
            }
            cnt.wait();

            // Merge results from all tasks into one single result container.
            assert(finalresults.Count <= 0);
            float required_square_error = 4.0f * lig.num_heavy_atoms; // Ligands with RMSD < 2.0 will be clustered into the same cluster.
            foreach (var result_container in result_containers)
            {
                foreach (result r in result_container)
                {
                    result.push(finalresults, r, required_square_error);
                }
                result_container.Clear();
            }
            finalresults.Sort(new ResultComparer());

            // If conformations are found, output them.
            num_confs = finalresults.Count;
            if (num_confs > 0)
            {
                // Adjust free energy relative to the best conformation and flexibility.
                result best_result = finalresults[0];
                float best_result_intra_e = best_result.e - best_result.f;
                //foreach (result r in results)
                for (int i = 0; i < finalresults.Count; i++)
                {
                    result r = finalresults[i];
                    r.e_nd = (r.e - best_result_intra_e) * lig.flexibility_penalty_factor;
                    r.rf = lig.calculate_rf_score(ref r, ref rec, ref f);
                }
                id_score = best_result.e_nd;
                rf_score = best_result.rf;

                // Write models to file.
                List<string> outputlines = new List<string>();
                rec.write_models(ref outputlines);
                lig.write_models(ref outputlines, ref finalresults, -1, ref rec);
                File.WriteAllLines(output_ligand_path, outputlines.ToArray());

                // Clear the results of the current ligand.
                finalresults.Clear();
            }

            return output_ligand_path;
        }
    }

}