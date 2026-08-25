require('dotenv').config();
const mongoose = require('mongoose');
const bcrypt = require('bcryptjs');
const User = require('./models/User');

async function createAdmin() {
  try {
    await mongoose.connect(process.env.MONGO_URI);
    console.log('✅ Connecté à MongoDB');

    const email = 'admin2@kyntuscq.com';
    const password = 'admin123';
    const passwordHash = await bcrypt.hash(password, 10);

    const admin = new User({
      name: 'Admin Principal',
      email,
      passwordHash,
      role: 'admin',
    });

    await admin.save();
    console.log(`🟢 Admin créé : ${email} / ${password}`);

    mongoose.connection.close();
  } catch (err) {
    console.error('❌ Erreur :', err.message);
  }
}

createAdmin();
