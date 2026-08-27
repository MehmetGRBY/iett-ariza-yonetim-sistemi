-- fault_categories tablosundaki her aktif alt arıza türü için onaylı çözüm rehberi oluşturur.
-- Başlık-kategori eşleşmesi daha önce oluşturulmuşsa tekrar eklemez; script güvenle yeniden çalıştırılabilir.
BEGIN;

SET LOCAL search_path TO fault_management, public;

WITH solution_data(category_name, root_code, symptoms, solution_steps, safety_notes, estimated_minutes) AS (
VALUES
('Direksiyon Arızası','MATERIAL',
 $$Direksiyonda boşluk, yön kararsızlığı, dönüş sırasında takılma, titreşim veya sürücünün aracı hatta tutmakta zorlanması.$$, 
 $$1. Aracı düz ve güvenli alana alın, tekerlekleri sabitleyin.
2. Direksiyon kutusu, kolon, rot kolları ve mafsallarda boşluk kontrolü yapın.
3. Hidrolik/elektrikli destek sisteminin hata kodlarını ve beslemesini ölçün.
4. Gevşek bağlantıları tork değerine göre sıkın; aşınmış mafsal veya rot elemanını değiştirin.
5. Rot ayarı yapıp düşük hızlı yön kararlılığı ve tam tur dönüş testi uygulayın.$$, 
 $$Direksiyon kontrolü kaybolabilecek araç trafiğe çıkarılmamalıdır. Araç kaldırılacaksa sehpa kullanılmalı, yalnız kriko üzerinde çalışılmamalıdır.$$,120),
('Direksiyon Sertleşmesi','MAINTENANCE',
 $$Direksiyonun özellikle düşük hızda ağırlaşması, pompa sesi, dönüşlerde kesikli destek veya gösterge panelinde direksiyon uyarısı.$$, 
 $$1. Lastik basınçlarını ve ön takımda mekanik sıkışma olup olmadığını kontrol edin.
2. Hidrolik yağ seviyesini, rengini, kayış gerginliğini ve pompa basıncını ölçün.
3. Elektrikli sistemde sigorta, besleme gerilimi, açı ve tork sensörü hata kodlarını okuyun.
4. Kaçak veya düşük basınç varsa bağlantıyı onarın; uygun sıvıyla sistemi havasını alarak doldurun.
5. Direksiyon kuvveti ve dönüş sonrası toplama testi gerçekleştirin.$$, 
 $$Basınçlı hidrolik hattı motor çalışırken sökülmemeli; elektrikli direksiyon konnektörleri akü ayrılmadan açılmamalıdır.$$,75),
('Hidrolik Kaçağı','MATERIAL',
 $$Araç altında yağ izi, direksiyon desteğinde azalma, pompa uğultusu, haznede sıvı eksilmesi veya hortum çevresinde ıslaklık.$$, 
 $$1. Kaçağın direksiyon kutusu, pompa, hazne, hortum veya bağlantı noktasından geldiğini temizleyerek tespit edin.
2. Hat basıncını boşaltın ve hasarlı hortum, kelepçe, conta ya da rakoru yenileyin.
3. Üretici standardındaki hidrolik sıvıyı seviyesine kadar doldurun.
4. Direksiyonu iki yöne çevirerek sistem havasını alın.
5. Basınç altında tekrar kaçak kontrolü ve yol testi yapın.$$, 
 $$Sıcak ve basınçlı yağ cilt yaralanmasına neden olabilir. Dökülen sıvı hemen temizlenmeli ve çevre prosedürüne göre toplanmalıdır.$$,90),
('Akü','ELECTRICAL',
 $$Marş motorunun dönmemesi veya yavaş dönmesi, aydınlatmanın zayıflaması, düşük voltaj uyarısı ve elektrikli sistemlerin devreye girmemesi.$$, 
 $$1. Akü kutup başlarını, kablo sıkılığını ve oksitlenmeyi gözle kontrol edin.
2. Dinlenme gerilimi, marş anı gerilimi ve yük testi değerlerini ölçün.
3. Alternatör şarj gerilimini ve kaçak akımı kontrol edin.
4. Kutup başlarını temizleyip sıkın; kapasitesini kaybetmiş aküyü eşdeğer özellikte yenileyin.
5. Şarj sistemi ve yeniden marş testiyle sonucu doğrulayın.$$, 
 $$Kutup başlarında kısa devre oluşturacak metal eşya kullanılmamalı; sökmede önce eksi, takmada önce artı kutup bağlanmalıdır.$$,45),
('Aydınlatma','ELECTRICAL',
 $$Far, stop, sinyal, iç aydınlatma veya plaka lambalarının hiç çalışmaması, titremesi ya da düşük şiddette yanması.$$, 
 $$1. Arızalı aydınlatma grubunu ve sorunun tekil mi ortak mı olduğunu belirleyin.
2. Ampul/LED modülü, sigorta ve röleyi kontrol edin.
3. Sokette besleme gerilimi, şase sürekliliği ve kablo direnci ölçün.
4. Arızalı elemanı değiştirin; oksitli soketi temizleyip kablo hasarını yalıtımlı biçimde onarın.
5. Tüm dış aydınlatma fonksiyonlarını ve gösterge uyarılarını test edin.$$, 
 $$Dış aydınlatması eksik araç gece veya düşük görüş koşullarında sefere verilmemelidir. Uygun amper dışında sigorta kullanılmamalıdır.$$,40),
('Elektrik Tesisatı','ELECTRICAL',
 $$Birden fazla elektrikli donanımın kesilmesi, sigorta atması, yanık kokusu, düzensiz voltaj veya kablo ve soketlerde ısınma.$$, 
 $$1. Arızalı devrenin şemasını inceleyip doğru sigorta ve röleyi belirleyin.
2. Aküyü güvenli biçimde ayırın; tesisatı ezilme, sürtünme, nem ve yanık izi açısından kontrol edin.
3. Kısa devre, şase kaçağı ve süreklilik ölçümlerini yapın.
4. Hasarlı kablo bölümünü otomotiv standardında yenileyin; soket ve izolasyonu onarın.
5. Akımı kademeli vererek devre yükünü ve bütün tüketicileri test edin.$$, 
 $$Yanık kokusu veya ısınma varsa enerji derhal kesilmelidir. Kablo köprüleme ve yüksek amperli sigorta kullanımı yangın riski doğurur.$$,150),
('Gösterge Paneli','ELECTRICAL',
 $$Panelin açılmaması, göstergelerin yanlış değer vermesi, ekranın kesilmesi veya sürekli uyarı lambası yanması.$$, 
 $$1. Akü gerilimi, panel sigortası, şase ve konnektörleri kontrol edin.
2. Teşhis cihazıyla gösterge paneli ve CAN haberleşme hata kodlarını okuyun.
3. Sensör verisini panelde görülen değerle karşılaştırın.
4. Soket temasını düzeltin; yazılım kalibrasyonu uygulayın veya arızalı sensör/panel modülünü yenileyin.
5. Kontak çevrimi ve sürüş simülasyonuyla tüm göstergeleri doğrulayın.$$, 
 $$Hız, fren basıncı veya kritik motor değerleri doğru görüntülenmiyorsa araç sefere çıkarılmamalıdır.$$,75),
('ABS Arızası','ELECTRICAL',
 $$ABS uyarı lambası, sert frenlemede tekerlek kilitlenmesi, çekiş kontrol uyarısı veya sensör hata kodu.$$, 
 $$1. ABS beynindeki hata kodlarını ve canlı tekerlek hızlarını okuyun.
2. Sensör, impuls halkası, kablo ve soketleri kir, hasar ve açıklık açısından kontrol edin.
3. Sensör direnci/beslemesi ile fren hidroliği seviyesini ölçün.
4. Hasarlı sensör, kablo veya impuls halkasını değiştirip hata kodlarını silin.
5. Kontrollü alanda ABS devreye giriş ve fren dengesi testi yapın.$$, 
 $$ABS arızalı araçta kilitlenme riski artar. Test yalnız kapalı ve güvenli alanda, emniyet kemeri takılı olarak yapılmalıdır.$$,120),
('Balata','WEAR',
 $$Fren sırasında sürtme sesi, balata kokusu, uzayan durma mesafesi, pedal titreşimi veya balata aşınma uyarısı.$$, 
 $$1. Tekerlekleri söküp balata kalınlığını ve iki taraflı aşınma farkını ölçün.
2. Disk yüzeyi, kalınlığı, kaliper kızakları ve piston hareketini kontrol edin.
3. Limit altındaki balataları aks bazında takım olarak değiştirin.
4. Kızakları uygun ürünle temizleyip yağlayın; disk uygunsuzsa yenileyin.
5. Pedal basıncı oluşturun, alıştırma prosedürü ve fren dengesi testi uygulayın.$$, 
 $$Fren tozu basınçlı havayla dağıtılmamalı; uygun maske ve fren temizleyici kullanılmalıdır. Sağ-sol balatalar birlikte değiştirilmelidir.$$,120),
('Fren Tutmuyor','MATERIAL',
 $$Pedalın tabana yaklaşması, aracın yavaşlamaması, tek tarafa çekme, hava basıncı uyarısı veya fren hidroliği kaybı.$$, 
 $$1. Aracı hareket ettirmeden takozlayın ve fren sistemindeki gözle görünür kaçağı kontrol edin.
2. Hava/hidrolik basıncı, ana merkez, kaliperler, hortumlar ve balata-disk durumunu ölçün.
3. Kaçak veya arızalı parçayı yenileyin; sistemi uygun sıvıyla doldurup havasını alın.
4. Fren basıncı, pedal sertliği ve akslar arası dengeyi test cihazıyla doğrulayın.
5. Kapalı alanda düşük hızlı fren testi sonrası kontrollü yol testi yapın.$$, 
 $$Fren tutmayan araç kesinlikle sürülmemeli ve çekiciyle taşınmalıdır. Basınç oluşmadan araç altına girilmemelidir.$$,180),
('Hava Basıncı','MATERIAL',
 $$Fren hava basıncı ikazı, basıncın geç dolması, kompresörün sürekli çalışması veya hava kaçağı sesi.$$, 
 $$1. Gösterge ve teşhis cihazından devre basınçlarını karşılaştırın.
2. Kompresör, hava kurutucu, tank tahliyeleri, valfler ve hortumlarda kaçak testi yapın.
3. Sabun köpüğü veya uygun kaçak cihazıyla sızıntı noktasını belirleyin.
4. Hasarlı hortum, rakor, valf veya kurutucu kartuşunu değiştirin.
5. Basınç dolum süresi, kesme basıncı ve fren uygulamasında basınç düşümünü ölçün.$$, 
 $$Hava basıncı güvenli seviyeye ulaşmadan araç hareket ettirilmemelidir. Basınçlı bağlantılar sistem boşaltılmadan sökülmemelidir.$$,120),
('Ayna Hasarı','EXTERNAL',
 $$Ayna camında kırık/çatlak, gövdede gevşeme, ayar motorunun çalışmaması veya görüş alanının bozulması.$$, 
 $$1. Ayna gövdesi, bağlantı ayağı, cam, ısıtma ve ayar motorunu kontrol edin.
2. Kapı bağlantı noktalarında deformasyon ve kablo hasarını inceleyin.
3. Hasarlı cam veya komple ayna grubunu değiştirip bağlantıları torklayın.
4. Elektrikli ayar ve ısıtma fonksiyonlarını test edin.
5. Sürücü konumundan kör nokta ve görüş açısı ayarını doğrulayın.$$, 
 $$Yetersiz yan görüşle araç sefere çıkarılmamalıdır. Kırık cam parçalarına karşı eldiven ve gözlük kullanılmalıdır.$$,45),
('Cam Hasarı','EXTERNAL',
 $$Ön/yan camda çatlak, kırık, görüş alanında bozulma, su alma veya cam mekanizmasının sıkışması.$$, 
 $$1. Hasarın sürücü görüş alanını ve camın yapısal bütünlüğünü etkileyip etkilemediğini değerlendirin.
2. Küçük onarılabilir çatlağı reçineyle onarın; ilerlemiş hasarda camı değiştirin.
3. Fitil, yapıştırıcı yüzey ve drenaj kanallarını temizleyin.
4. Yeni camı uygun primer ve yapıştırıcıyla monte edip kürlenme süresini bekleyin.
5. Su sızdırmazlık, görüş ve varsa ısıtma/sensör kontrolü yapın.$$, 
 $$Görüşü engelleyen veya ilerleyen ön cam çatlağıyla araç kullanılmamalı; cam değişiminde kesilmeye dayanıklı eldiven kullanılmalıdır.$$,120),
('Kaporta Hasarı','EXTERNAL',
 $$Gövde panelinde ezik, keskin kenar, gevşek parça, kapı/kapak hizasızlığı veya dış donanımın yerinden çıkması.$$, 
 $$1. Hasarlı bölgeyi şasi, kapı mekanizması ve yolcu güvenliği açısından inceleyin.
2. Gevşek parçaları sökün; panel ve bağlantı noktalarını ölçüp düzeltin.
3. Keskin kenarları giderin, gerekli sabitleme elemanlarını yenileyin.
4. Korozyon koruması ve yüzey işlemini uygulayın.
5. Kapı/kapak işlevi, dış ölçüler ve güvenli geçiş kontrolünü tamamlayın.$$, 
 $$Yolcuya temas edebilecek keskin veya gevşek kaporta parçası varken araç işletmeye verilmemelidir.$$,180),
('Koltuk Arızası','MATERIAL',
 $$Koltuğun gevşemesi, kırık iskelet, yırtık yüzey, keskin parça veya sabitleme cıvatalarında oynama.$$, 
 $$1. Koltuk iskeleti, zemin bağlantısı, tutamak ve kaplamayı kontrol edin.
2. Gevşek bağlantıları üretici torkunda sıkın.
3. Kırık iskelet veya bağlantı elemanını onaylı parçayla değiştirin.
4. Keskin yüzeyleri giderip kaplamayı yenileyin.
5. Statik yük ve sallanma testiyle koltuğun güvenliğini doğrulayın.$$, 
 $$Gevşek veya keskin parçası bulunan koltuk kullanım dışı işaretlenmeli; güvenli hâle gelmeden yolcu kullanımına açılmamalıdır.$$,60),
('Kapı Açılmıyor','MATERIAL',
 $$Kapı komut almasına rağmen açılmıyor, yarıda kalıyor, mekanizmadan ses geliyor veya acil açma sistemi zor çalışıyor.$$, 
 $$1. Hava/elektrik beslemesini, kumanda sinyalini ve kapı kilidini kontrol edin.
2. Mekanizma, kızak, piston ve mafsallarda sıkışma arayın.
3. Sensör ve valf hata kodlarını okuyup kablo/soket kontrolü yapın.
4. Sıkışmayı giderin; arızalı valf, piston, motor veya sensörü değiştirin.
5. Normal ve acil açma dâhil en az beş çevrim fonksiyon testi yapın.$$, 
 $$Acil tahliyeyi etkileyen kapı arızasında araç yolcu taşıyamaz. Basınçlı kapı mekanizmasında enerji kesilmeden çalışma yapılmamalıdır.$$,90),
('Kapı Kapanmıyor','MATERIAL',
 $$Kapının açık kalması, kapanıp geri açılması, kilitlenmemesi veya kapı açık uyarısının sönmemesi.$$, 
 $$1. Kapı yolunda engel, mekanik hizasızlık ve lastik fitil deformasyonu kontrolü yapın.
2. Kapanma sensörü, sıkışma koruması ve kilit mekanizmasını test edin.
3. Hava basıncı/elektrik beslemesi ile valf veya motor hareketini ölçün.
4. Ayar yapın; arızalı sensör, kilit ya da tahrik elemanını yenileyin.
5. Sıkışma koruması ve hareket hâli kilidi dâhil çevrim testi uygulayın.$$, 
 $$Kapanmayan veya kilitlenmeyen kapıyla araç hareket ettirilmemeli; test sırasında kapı hareket alanı boş tutulmalıdır.$$,90),
('Kapı Sensörü Arızası','ELECTRICAL',
 $$Kapı açık/kapalı bilgisinin yanlış görünmesi, kapının gereksiz geri açılması veya araç hareket izninin oluşmaması.$$, 
 $$1. Teşhis cihazından kapı sensörü canlı verisini izleyin.
2. Sensör hizası, mıknatıs/anahtar mesafesi, soket ve kabloları kontrol edin.
3. Besleme gerilimi ve sinyal değişimini ölçün.
4. Sensörü hizalayın veya değiştirin; kablo hasarını onarın.
5. Kapı çevrimlerinde gösterge ve hareket kilidi sinyalini doğrulayın.$$, 
 $$Kapı durumu doğrulanmadan hareket kilidi devre dışı bırakılmamalı veya sensör köprülenmemelidir.$$,60),
('Havalandırma Arızası','ELECTRICAL',
 $$Kabinde hava dolaşımının yetersiz olması, fanın çalışmaması, düzensiz hız veya kanallardan anormal ses gelmesi.$$, 
 $$1. Fan sigortası, rölesi, besleme ve kumanda panelini kontrol edin.
2. Filtreleri, hava kanallarını ve giriş ızgaralarını tıkanma açısından inceleyin.
3. Fan motoru akımı ve hız kontrol modülünü ölçün.
4. Filtreyi temizleyin/değiştirin; arızalı motor, röle veya kontrol modülünü yenileyin.
5. Tüm fan kademelerinde hava debisi ve ses kontrolü yapın.$$, 
 $$Fan hareketliyken koruyucu kapak açılmamalı; elektrik ölçümü dışında enerji kesilmelidir.$$,75),
('Isıtma Çalışmıyor','MAINTENANCE',
 $$Kabine sıcak hava gelmemesi, motor sıcak olduğu hâlde soğuk üfleme, düzensiz ısı veya kalorifer sıvısı kokusu.$$, 
 $$1. Motor soğutma sıvısı seviyesi ve çalışma sıcaklığını kontrol edin.
2. Kalorifer valfi, sirkülasyon pompası, hortumlar ve ısı eşanjörünü inceleyin.
3. Kumanda sinyali ve fan çalışmasını ölçün.
4. Sistemin havasını alın; tıkalı filtre/eşanjörü temizleyin veya arızalı valf/pompayı değiştirin.
5. Kabin çıkış sıcaklığı ve kaçak kontrolü yapın.$$, 
 $$Sıcak ve basınçlı soğutma sistemi kapağı açılmamalıdır. Sıvı kaçağı yolcu alanına ulaşıyorsa araç kullanılmamalıdır.$$,120),
('Klima Çalışmıyor','MATERIAL',
 $$Soğuk hava üretmemesi, kompresörün devreye girmemesi, düzensiz soğutma veya klima hata uyarısı.$$, 
 $$1. Kumanda paneli, sigorta, röle ve kompresör devreye girişini kontrol edin.
2. Soğutucu gaz basınçlarını, kaçak izlerini ve kondenser fanlarını ölçün.
3. Filtre, evaporatör ve kondenseri kirlenme açısından inceleyin.
4. Kaçağı giderin; arızalı sensör, valf veya kompresör elemanını değiştirin.
5. Vakum ve uygun miktarda gaz dolumu sonrası çıkış sıcaklığı testi yapın.$$, 
 $$Soğutucu gaz atmosfere salınmamalı; geri kazanım cihazı ve uygun kişisel koruyucu kullanılmalıdır.$$,150),
('Düşük Hava Basıncı','MAINTENANCE',
 $$Lastik basınç uyarısı, direksiyon çekmesi, lastikte gözle görünür çökme veya yakıt tüketiminde artış.$$, 
 $$1. Tüm lastikleri soğukken kalibreli manometreyle ölçün.
2. Lastik yüzeyi, sibop ve jant kenarında kaçak kontrolü yapın.
3. Üretici yük tablosuna göre doğru basınca getirin.
4. Kaçak varsa sibobu yenileyin veya lastiği sökerek uygun onarım uygulayın.
5. Basınç sensörü varsa kalibrasyon yapıp kısa sürüşle uyarıyı doğrulayın.$$, 
 $$Aşırı düşük basınçlı lastikle sürüş yapılmamalı; şişirme sırasında lastiğin yanında değil güvenli doğrultuda durulmalıdır.$$,30),
('Jant Hasarı','EXTERNAL',
 $$Direksiyon titreşimi, hava kaçırma, jantta eğilme/çatlak, bijon çevresinde deformasyon veya tekerlek balanssızlığı.$$, 
 $$1. Jantı temizleyip iç-dış yüzeyde çatlak ve darbe izi kontrolü yapın.
2. Salgı ölçümü ve hava sızdırmazlık testi uygulayın.
3. Bijon delikleri ve göbek oturma yüzeyini inceleyin.
4. Limit dışı eğri veya çatlak jantı değiştirin; uygun durumdaysa yetkili yöntemle düzeltin.
5. Tekerlek balansı ve bijon torku sonrası yol testi yapın.$$, 
 $$Çatlak, bijon yatağı bozuk veya limit dışı jant onarılarak sefere verilmemeli; yenisiyle değiştirilmelidir.$$,90),
('Lastik Patlaması','EXTERNAL',
 $$Ani basınç kaybı, aracın bir yana çekmesi, vuruntu, lastik basınç uyarısı veya lastik yüzeyinde yırtılma.$$, 
 $$1. Aracı güvenli alanda sabitleyip hasarlı tekerleği ve çevre parçaları kontrol edin.
2. Lastiği sökerek karkas, yanak, sırt ve jant hasarını inceleyin.
3. Onarım sınırındaysa uygun yama-mantar işlemi uygulayın; yanak/karkas hasarında lastiği değiştirin.
4. Doğru basınçta şişirip balans yapın ve bijonları torklayın.
5. Diğer lastiklerde basınç ve yabancı cisim kontrolü yapın.$$, 
 $$Yol kenarında güvenli alan oluşturulmadan çalışma yapılmamalı; kriko yanında mutlaka taşıma kapasitesine uygun sehpa kullanılmalıdır.$$,60),
('Anormal Ses','WEAR',
 $$Motor bölgesinden vuruntu, sürtünme, ıslık, metalik tıkırtı veya devirle artan alışılmadık ses gelmesi.$$, 
 $$1. Sesin motor devri, araç hızı, yük ve sıcaklıkla ilişkisini belirleyin.
2. Kayış, kasnak, rulman, fan, egzoz ve motor bağlantılarını kontrol edin.
3. Stetoskop ve teşhis verileriyle ses kaynağını bölgesel olarak tespit edin.
4. Gevşek bağlantıyı sıkın; aşınmış rulman, kayış veya ilgili parçayı değiştirin.
5. Sabit ve yol yükü altında sesin kesildiğini doğrulayın.$$, 
 $$Yağ basıncı düşmesi veya ağır metalik vuruntu eşlik ediyorsa motor hemen durdurulmalı ve yeniden çalıştırılmamalıdır.$$,120),
('Hararet','MAINTENANCE',
 $$Sıcaklık göstergesinin yükselmesi, hararet uyarısı, buhar, soğutma sıvısı kokusu veya motor gücünde düşme.$$, 
 $$1. Motoru durdurup güvenli şekilde soğumasını bekleyin.
2. Soğutma sıvısı, hortumlar, radyatör, kapak ve devirdaim pompasında kaçak kontrolü yapın.
3. Fan, termostat ve sıcaklık sensörünün çalışma değerlerini test edin.
4. Kaçağı giderin; arızalı termostat, pompa, fan veya hortumu değiştirin.
5. Sistemi uygun karışımla doldurup havasını alın ve çalışma sıcaklığını izleyin.$$, 
 $$Sıcak motorda radyatör kapağı kesinlikle açılmamalıdır. Hararetli motorla sürüş ciddi motor hasarı ve yangın riski oluşturur.$$,150),
('Motor Çalışmıyor','ELECTRICAL',
 $$Marşın hiç dönmemesi, motorun dönüp ateşlememesi, seyir sırasında stop etmesi veya motor arıza uyarısı.$$, 
 $$1. Akü gerilimi, marş sistemi ve ana sigortaları kontrol edin.
2. Teşhis cihazıyla motor kontrol ünitesi hata kodlarını ve canlı verileri okuyun.
3. Yakıt basıncı, ateşleme/enjeksiyon, krank sensörü ve hava beslemesini ölçün.
4. Arızalı elektrik bağlantısı, sensör, yakıt elemanı veya mekanik parçayı onarın/değiştirin.
5. Soğuk-sıcak marş, rölanti ve yük altında çalışma testi yapın.$$, 
 $$Yakıt kaçağı, yoğun duman veya mekanik kilitlenme şüphesinde motor çalıştırılmamalı; araç çekiciyle alınmalıdır.$$,180),
('Yağ Kaçağı','MATERIAL',
 $$Araç altında yağ izi, motor üzerinde ıslaklık, yağ seviyesi düşmesi, yanık yağ kokusu veya düşük yağ basıncı uyarısı.$$, 
 $$1. Yağ seviyesini ölçüp motoru temizleyerek kaçağın kaynağını belirleyin.
2. Karter, filtre, tapa, conta, keçeler ve yağ hatlarını kontrol edin.
3. Gevşek bağlantıyı torklayın; hasarlı conta, keçe, hortum veya parçayı değiştirin.
4. Uygun özellikte yağı doğru seviyeye tamamlayın.
5. Motoru farklı devirlerde çalıştırıp basınç ve yeniden kaçak kontrolü yapın.$$, 
 $$Yağ sıcak egzoz yüzeyine ulaşıyorsa yangın riski vardır. Düşük yağ basıncı uyarısında motor derhal durdurulmalıdır.$$,120),
('Amortisör Arızası','WEAR',
 $$Aşırı salınım, kasiste vuruntu, yol tutuşta zayıflama, düzensiz lastik aşınması veya amortisörde yağ kaçağı.$$, 
 $$1. Amortisör gövdesi, burçlar ve bağlantılarda kaçak/boşluk kontrolü yapın.
2. Araç yüksekliği ve sağ-sol salınımını karşılaştırın.
3. Süspansiyon test cihazıyla sönümleme değerlerini ölçün.
4. Arızalı amortisörü aynı aks üzerinde çift olarak, burç ve bağlantılarıyla yenileyin.
5. Tork, rot ve kontrollü yol testiyle sonucu doğrulayın.$$, 
 $$Süspansiyon boşaltılmadan bağlantılar sökülmemeli; ağır araç uygun kaldırma ekipmanı ve sehpalarla desteklenmelidir.$$,150),
('Araç Yükseklik Arızası','ELECTRICAL',
 $$Aracın bir yana yatması, normal seviyeye yükselmemesi, seviye uyarısı veya kneeling sisteminin çalışmaması.$$, 
 $$1. Hava basıncı ve körüklerde kaçak kontrolü yapın.
2. Yükseklik sensörlerinin mekanik bağlantılarını ve canlı değerlerini karşılaştırın.
3. Seviye valfi, solenoid ve kontrol ünitesi hata kodlarını inceleyin.
4. Kaçağı giderin; arızalı sensör, valf veya kabloyu değiştirin ve kalibrasyon yapın.
5. Boş ve yüklü seviye, kneeling ve sürüş yüksekliği testlerini tamamlayın.$$, 
 $$Araç altında çalışma öncesi gövde mekanik olarak desteklenmelidir; hava boşalması aracın aniden alçalmasına neden olabilir.$$,120),
('Körük Arızası','MATERIAL',
 $$Araçta yana yatma, hava kaçak sesi, körük yüzeyinde çatlak, süspansiyon basıncının düşmesi veya sert sürüş.$$, 
 $$1. Körükleri çatlak, sürtünme, katlanma ve bağlantı kaçağı açısından kontrol edin.
2. Hava devresini boşaltıp aracı mekanik olarak destekleyin.
3. Hasarlı körüğü, conta ve bağlantı elemanlarıyla birlikte değiştirin.
4. Sistemi basınçlandırıp sabun köpüğüyle sızdırmazlık kontrolü yapın.
5. Araç yüksekliği kalibrasyonu ve yol konfor testi uygulayın.$$, 
 $$Basınçlı körük üzerinde işlem yapılmamalı; araç yalnız hava süspansiyonu üzerinde bırakılmamalıdır.$$,180),
('Vites Geçiş Problemi','MAINTENANCE',
 $$Vitesin geç devreye girmesi, sarsıntılı geçiş, viteste kalma, boşa düşme veya şanzıman uyarısı.$$, 
 $$1. Şanzıman hata kodlarını, yağ sıcaklığını ve seçici konumlarını okuyun.
2. Yağ seviyesini, rengini, kokusunu ve kaçakları kontrol edin.
3. Elektrik soketleri, vites seçici, sensör ve solenoid değerlerini ölçün.
4. Yağ/filtre bakımını yapın; arızalı sensör, solenoid veya bağlantıyı yenileyin.
5. Adaptasyon işlemi ve farklı yüklerde kontrollü vites geçiş testi yapın.$$, 
 $$Araç vitese geçerken çevresinde personel bulunmamalı; tekerlekler takozlanmalı ve test yetkili sürücüyle yapılmalıdır.$$,150),
('Şanzıman Arızası','MATERIAL',
 $$Hareket iletilememesi, metalik ses, ağır sarsıntı, yanık yağ kokusu, sürekli arıza modu veya şanzıman hata uyarısı.$$, 
 $$1. Teşhis kodları, yağ basıncı, sıcaklık ve giriş-çıkış devirlerini kaydedin.
2. Yağ ve filtrede metal parçacık/kirlenme kontrolü yapın.
3. Elektrik tesisatı, kontrol ünitesi, valf bloğu ve mekanik aktarma bağlantılarını inceleyin.
4. Arıza bulgusuna göre valf bloğu, kavrama paketi, sensör veya şanzıman grubunu onarın/değiştirin.
5. Adaptasyon, kaçak, basınç ve kademeli yük yol testi gerçekleştirin.$$, 
 $$Ağır mekanik ses veya hareket kaybında araç sürülmemeli, çekiciyle taşınmalıdır. Şanzıman sökümünde uygun taşıma krikosu kullanılmalıdır.$$,360),
('Şanzıman Yağ Kaçağı','MATERIAL',
 $$Şanzıman altında yağ izi, yağ seviyesi düşmesi, yanık koku, geçişlerde bozulma veya gövde üzerinde ıslaklık.$$, 
 $$1. Şanzıman yüzeyini temizleyip kaçağın tapa, karter, keçe, soğutucu hat veya contadan geldiğini belirleyin.
2. Yağ seviyesini ve sıcaklığa bağlı ölçüm prosedürünü uygulayın.
3. Hasarlı conta, keçe, hortum veya rakoru yenileyin; bağlantıları torklayın.
4. Üretici onaylı yağı doğru sıcaklık ve seviyede doldurun.
5. Çalışma sıcaklığında kaçak ve vites geçiş kontrolü yapın.$$, 
 $$Sıcak şanzıman yağı yanık riski oluşturur. Düşük yağ seviyesiyle araç hareket ettirilmemelidir.$$,150)
),
admin_user AS (
    SELECT u.id, u.role_id
      FROM app_users u
      JOIN roles r ON r.id = u.role_id
     WHERE u.is_active AND r.name = 'Admin'
     ORDER BY u.id
     LIMIT 1
),
prepared AS (
    SELECT fc.id AS category_id,
           fc.name AS category_name,
           sd.root_code,
           sd.symptoms,
           sd.solution_steps,
           sd.safety_notes,
           sd.estimated_minutes,
           rc.id AS root_cause_id,
           au.id AS admin_id,
           au.role_id AS admin_role_id,
           source_report.id AS source_report_id,
           fc.name || ' - Kontrol ve Çözüm Rehberi' AS article_title
      FROM solution_data sd
      JOIN fault_categories fc ON fc.name = sd.category_name
                               AND fc.parent_category_id IS NOT NULL
                               AND fc.is_active
      JOIN root_causes rc ON rc.code = sd.root_code AND rc.is_active
      CROSS JOIN admin_user au
      LEFT JOIN LATERAL (
          SELECT rr.id
            FROM repair_reports rr
            JOIN fault_assignments fa ON fa.id = rr.fault_assignment_id
            JOIN faults f ON f.id = fa.fault_id
           WHERE f.fault_category_id = fc.id
             AND rr.is_active AND rr.is_submitted
             AND rr.result IN ('RESOLVED', 'REPAIRED')
           ORDER BY rr.completed_at DESC, rr.id DESC
           LIMIT 1
      ) source_report ON true
)
INSERT INTO solution_articles
    (fault_category_id, root_cause_id, source_repair_report_id, title,
     symptoms, solution_steps, safety_notes, estimated_minutes,
     approval_status, created_by_user_id, approved_by_user_id,
     approved_at, is_active, created_at)
SELECT p.category_id, p.root_cause_id, p.source_report_id, p.article_title,
       p.symptoms, p.solution_steps, p.safety_notes, p.estimated_minutes,
       'APPROVED', p.admin_id, p.admin_id, clock_timestamp(), true, clock_timestamp()
  FROM prepared p
 WHERE NOT EXISTS (
       SELECT 1 FROM solution_articles sa
        WHERE sa.fault_category_id = p.category_id
          AND sa.title = p.article_title);

-- Toplu içerik oluşturma işlemi denetlenebilir tek bir audit kaydıyla özetlenir.
INSERT INTO audit_logs
    (user_id, role_id, action, entity_type, entity_id, new_values, description, created_at)
SELECT au.id, au.role_id, 'SOLUTION_LIBRARY_SEEDED', 'solution_articles', NULL,
       jsonb_build_object('activeSolutionCount',
           (SELECT COUNT(*) FROM solution_articles WHERE is_active)),
       'Aktif alt arıza kategorileri için ayrıntılı çözüm kütüphanesi oluşturuldu.',
       clock_timestamp()
  FROM app_users au
  JOIN roles ar ON ar.id = au.role_id
 WHERE au.is_active AND ar.name = 'Admin'
 ORDER BY au.id
 LIMIT 1;

COMMIT;

-- Kategori kapsamı ile oluşturulan çözüm sayısının eşleşmesini doğrular.
SELECT
    (SELECT COUNT(*) FROM fault_management.fault_categories
      WHERE parent_category_id IS NOT NULL AND is_active) AS aktif_alt_ariza,
    (SELECT COUNT(DISTINCT fault_category_id) FROM fault_management.solution_articles
      WHERE is_active) AS cozum_olan_ariza,
    (SELECT COUNT(*) FROM fault_management.solution_articles
      WHERE is_active) AS toplam_cozum;
